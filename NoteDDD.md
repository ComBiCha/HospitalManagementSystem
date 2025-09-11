Document: Domain-Driven Design trong Hospital Management System

Mục lục (tóm tắt)
Giới thiệu DDD (định nghĩa)
Kiến trúc tổng quan (4 layer)
Cấu trúc thư mục chi tiết (skeleton)
Domain modeling — Entities / ValueObjects / Aggregates / Invariants / Domain Services / Factories
Application Layer — Commands / Queries / Handlers / Orchestration
Infrastructure Layer — Repositories, EF, Redis, RabbitMQ, File storage, Outbox
Events — Domain Event vs Integration Event (rõ ràng) + mẫu JSON + versioning + routing
Messaging patterns: publish/subscribe, outbox, idempotency, dead-letter
Saga / Process Manager (ví dụ booking + payment)
Patterns đã dùng & ví dụ code (Repository, Decorator, Factory, Strategy, Outbox)
Testing / CI / Observability / Deployment notes
Event Catalog mẫu (bảng)
Checklist refactor + templates (APIs, Event spec, Sequence diagram)
Kết luận & bước tiếp theo