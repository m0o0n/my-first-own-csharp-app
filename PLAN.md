# Order Tracking — CQRS + Event Sourcing (Marten + Wolverine)

Учебный pet-проект. Цель — руками пощупать CQRS, Event Sourcing, Marten и Wolverine на C#/.NET.

## Домен

Заказ (`Order`) с жизненным циклом:

```
Placed → Paid → Shipped
   └────────→ Cancelled   (только пока не Shipped)
```

### Domain events

- `OrderPlaced { OrderId, CustomerId, Items[], TotalAmount, PlacedAt }`
- `OrderPaid { OrderId, PaidAt, PaymentReference }`
- `OrderShipped { OrderId, ShippedAt, TrackingNumber }`
- `OrderCancelled { OrderId, CancelledAt, Reason }`

### Бизнес-правила (инварианты)

1. Нельзя оплатить заказ, который уже отменён или отгружен.
2. Нельзя отгрузить неоплаченный заказ.
3. Нельзя отменить заказ после отгрузки (`Shipped` — терминальный статус, возвраты — вне scope).
4. Нельзя повторно применить статус, который уже применён (например второй `Paid`).
5. Таймаут: если заказ не оплачен за N минут (например 15) — автоотмена, `Cancelled` с `Reason = "payment timeout"`.

### Query side (read models)

- Список заказов с текущим статусом.
- Детали одного заказа.
- (опционально) заказы, близкие к таймауту.

## Стек

- .NET 10 / ASP.NET Core Minimal API
- PostgreSQL — локально, уже поднят, connection string в user-secrets (`DB_URL`)
- Marten — event store (write-side) + projections (read-side)
- Wolverine — message bus между Write и Read сервисами, саги для таймаутов
- RabbitMQ (docker) — транспорт для Wolverine

## Архитектура — Write/Read разнесены физически

Решили разносить не логически (папки), а по-настоящему — два отдельных процесса,
чтобы пощупать, как работает message bus между сервисами.

```
OrderTracking.sln
└── src/
    ├── OrderTracking.Contracts   — общие DTO событий (OrderPlaced, OrderPaid, OrderShipped, OrderCancelled)
    ├── OrderTracking.Write       — команды, Order aggregate, Marten event store, публикует события в RabbitMQ
    └── OrderTracking.Read        — projections, GET-эндпоинты, подписан на RabbitMQ, свой read-model store
```

- `Contracts` — небольшая сборка, на неё ссылаются оба сервиса (иначе Read не сможет
  десериализовать прилетевшее событие).
- Write и Read общаются **только через шину** (RabbitMQ), никаких прямых HTTP-вызовов
  друг к другу.
- RabbitMQ поднимается локально в docker:
  ```
  docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:4-management
  ```
  UI на `http://localhost:15672` (guest/guest) — удобно смотреть очереди/сообщения вживую.

## Как работаем

- Маленькими шагами, руками. Перед каждым куском кода — объяснение концепции.
- После каждой фазы — короткий чекпоинт: что понятно, что нет, что зависло.
- Не бежим вперёд по фазам, пока предыдущая не "показала" результат (curl / .http / SQL-запрос к БД).

## Фазы

### Фаза 0 — Подготовка ✅

- [x] Postgres локально
- [x] user-secrets с connection string
- [x] .gitignore (bin/obj вне репо)
- [x] RabbitMQ в docker
- [x] Solution + 3 проекта (`Contracts`, `Write`, `Read`), текущий `MyApi` встроен как основа под `Write`

### Фаза 1 — Голый Marten в Write: aggregate + events ✅

Цель: понять, как Marten хранит события и восстанавливает состояние из них.
Шины пока нет — работаем только с `Write`-сервисом.

- [x] Подключить Marten в `OrderTracking.Write` (`AddMarten`, connection string из конфига)
- [x] `Contracts`: 4 события (`OrderPlaced`, `OrderPaid`, `OrderShipped`, `OrderCancelled`) + `OrderItem`
- [x] `Write.Domain`: `Order` aggregate (класс с `Apply(...)` методами) + `OrderStatus`
- [x] Command-функции: `PlaceOrder`, `PayOrder`, `ShipOrder`, `CancelOrder`
  - загрузить aggregate через `FetchLatest`
  - проверить бизнес-правило (инвариант)
  - append нового события
  - `SaveChangesAsync`
- [x] Minimal API эндпоинты в `Write`, дёргающие эти команды
- [x] Проверка руками через Postman/curl: создать → оплатить → отгрузить → попытаться отменить (упало с 400, событие не записалось)

**Критерий готовности достигнут:** заказ проводится по всем статусам через HTTP, нарушение правила даёт ошибку без записи, в `mt_events` видна вся история (подтверждено запросом к БД).

### Фаза 2 — Wolverine + RabbitMQ: публикация событий из Write ✅

Цель: пощупать саму шину — увидеть, как событие уходит в очередь.

- [x] Поднять RabbitMQ в docker, глянуть UI (localhost:15672)
- [x] Подключить Wolverine в `Write` (`WolverineFx`, `WolverineFx.RabbitMQ`, `WolverineFx.RuntimeCompilation`), настроить `UseRabbitMq()` + `PublishAllMessages().ToRabbitExchange("orders")`
- [x] После каждого успешного command handler (все 4) — публикация через `IMessageBus.PublishAsync`
- [x] Проверка руками: временная очередь `orders-test`, забинженная на exchange `orders`, подтвердила что все 3-4 события реально долетают с полным JSON

**Критерий готовности достигнут.**

**Известный technical debt (не блокер, но отметить):** `SaveChangesAsync()` и `PublishAsync()` — два отдельных нетранзакционных шага. Если процесс упадёт между ними — событие попадёт в `mt_events`, но не долетит до шины. Правильное решение — transactional outbox через `WolverineFx.Marten` (сообщение пишется в ту же Postgres-транзакцию). Рассмотреть отдельно, не блокирует переход к Фазе 3.

### Фаза 3 — Read-side: подписка + projections

Цель: собрать read model из событий, прилетевших по шине (а не читая write-side БД напрямую).

- [ ] `OrderTracking.Read` — отдельный ASP.NET Core проект, свой Marten (может быть та же Postgres, отдельные таблицы/схема)
- [ ] Подключить Wolverine в `Read`, подписаться на очередь из Фазы 2
- [ ] Message handlers в `Read`: на каждое входящее событие — обновить `OrderSummary` (свою read-модель)
- [ ] `GET /orders`, `GET /orders/{id}` в `Read` — отдают то, что накопилось через шину
- [ ] Проверка руками: гоняем заказ через `Write`, смотрим что `Read` обновляется без прямого обращения к `Write`

**Критерий готовности:** можно выключить `Write`, и `Read` всё равно отдаёт последние известные данные — они живут в своей БД, независимо.

### Фаза 4 — Таймауты через Wolverine scheduled messages

Цель: авто-отмена неоплаченного заказа — уже средствами шины, а не поллингом.

- [ ] При `OrderPlaced` — запланировать scheduled message (например `CancelIfUnpaid`) через N минут
- [ ] Handler в `Write`: если заказ всё ещё `Placed` на момент срабатывания — выполнить `CancelOrder`
- [ ] Если успели оплатить раньше — просто ничего не делать (или явно отменить запланированное сообщение, если Wolverine это позволяет)
- [ ] Проверка руками: создать заказ, не оплачивать, подождать таймаут, увидеть `Cancelled` с `Reason = "payment timeout"`

### Фаза 5 (опционально) — Saga / process manager

- [ ] Если захочется усложнить флоу (например добавить резервирование склада как отдельный шаг) — оформить как Wolverine saga

### Фаза 6 — Тесты и cleanup (опционально)

- [ ] Unit-тесты на бизнес-правила aggregate (без БД, чистая логика)
- [ ] Integration-тест на полный флоу Write→RabbitMQ→Read (Testcontainers: Postgres + RabbitMQ)

### Фаза 7 (stretch goal) — Частичные платежи

Цель: потренироваться расширять уже существующий функционал, не ломая пройденные фазы.

Сейчас `OrderPaid` — разовый полный платёж (проверка `amount < TotalAmount` → reject, `>=` — проходит, переплата не блокируется). Частичные платежи — другая модель:

- [ ] `Order` аккумулирует `PaidAmount` по всем `OrderPaid` событиям (`+=`, не `=`)
- [ ] Переход в статус `Paid` — не в handler'е, а когда накопленная сумма `>= TotalAmount` (решение переезжает в `Apply` или в handler на основе пересчитанного состояния)
- [ ] Подумать нужен ли промежуточный статус (`PartiallyPaid`) в `OrderStatus`
- [ ] Как это скажется на `OrderSummary` read-model (Фаза 3) — показывать ли "оплачено X из Y"?
- [ ] Что с уже созданными в БД событиями старого формата — ломающее изменение или aддитивное? (см. обсуждение про эволюцию схемы событий)

## Открытые вопросы / решить по ходу

- ~~Какая БД под Marten~~ — **решено**: одна физическая БД `orders` (не дефолтная `postgres`), но разные Marten-схемы внутри: `write_orders` для Write-сервиса, `read_orders` для Read. Задаётся через `DatabaseSchemaName` при `AddMarten`.
- Формат сообщений в шине — сериализуем сами domain events из `Contracts`, или заводим отдельные "integration events" (более стабильный публичный контракт, отвязанный от внутренней модели `Write`)? Обсудим на Фазе 2.
