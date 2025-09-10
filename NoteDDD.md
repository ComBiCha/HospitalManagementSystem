API Layer
    Chỉ chứa:
        Controller
        Startup/Program
        Config, Dockerfile, launchSettings
        gRPC service stub nếu dùng

Application Layer
    Chỉ chứa:
        Service logic (manager, orchestrator, use case, application service)

Domain Layer
    Chỉ chứa:
        Entities
        Value Objects
        Events
        Interfaces (Repository, Strategy, Factory, Service, Payment, Notification, Storage, RabbitMQ, Cache, ...)

Infrastructure Layer
    Chỉ chứa:
        Implement của các interface ở Domain
        Adapter cho external service (Stripe, RabbitMQ, Redis, SeaweedFS, ...)
        Repository implement
        Strategy implement
        Payment method implement
        Notification channel implement
        Cache implement
        Storage implement
        DbContext

Domain-Driven Design (DDD) - Tổng Quan & Áp Dụng
1. Lý do sử dụng DDD
    Quản lý phức tạp nghiệp vụ: DDD giúp chia nhỏ hệ thống lớn thành các phần rõ ràng, tập trung vào nghiệp vụ (domain) thay vì chỉ code kỹ thuật.
    Tách biệt rõ ràng giữa nghiệp vụ và hạ tầng: Giúp code dễ bảo trì, mở rộng, test, onboard team mới.
    Dễ mở rộng, tích hợp: Khi cần thêm tính năng, chỉ cần mở rộng đúng layer mà không ảnh hưởng toàn bộ hệ thống.
    Đồng bộ với kiến trúc microservices: DDD là nền tảng tốt cho các hệ thống lớn, đa dịch vụ.

2. Ý nghĩa các Layer trong DDD
    a. Domain Layer
        Ý nghĩa: Là trung tâm của hệ thống, chứa toàn bộ logic nghiệp vụ, quy tắc, khái niệm cốt lõi.
        Chứa:
        Entities: Đối tượng nghiệp vụ (Patient, Doctor, Billing, Appointment, ...)
        Value Objects: Giá trị bất biến (Money, Address, ...)
        Events: Sự kiện nghiệp vụ (AppointmentCreatedEvent, PaymentProcessedEvent, ...)
        Interfaces: Định nghĩa các hành vi (Repository, Strategy, Factory, Service, Payment, Notification, Storage, RabbitMQ, Cache, ...)
        Không chứa:
        Code thực thi, logic kỹ thuật, adapter, external service.
    b. Application Layer
        Ý nghĩa: Điều phối luồng nghiệp vụ, xử lý các use-case, orchestrator, manager.
        Chứa:
        Service logic (manager, orchestrator, use case, application service)
        Không chứa:
        Entity, event, interface, implement.
    c. Infrastructure Layer
        Ý nghĩa: Thực thi các interface của Domain, kết nối với các hệ thống bên ngoài (DB, Redis, RabbitMQ, Stripe, File Storage, ...)
        Chứa:
        Implement của các interface ở Domain
        Adapter cho external service (Stripe, RabbitMQ, Redis, SeaweedFS, ...)
        Repository implement
        Strategy implement
        Payment method implement
        Notification channel implement
        Cache implement
        Storage implement
        DbContext
        Không chứa:
        Interface, entity, event, controller, service logic.
    d. API Layer
        Ý nghĩa: Giao tiếp với bên ngoài (client, frontend, service khác), nhận request, trả response.
        Chứa:
        Controller
        Startup/Program
        Config, Dockerfile, launchSettings
        gRPC service stub nếu dùng
        Không chứa:
        Logic nghiệp vụ, implement, entity, event.

3. Luồng đi của dữ liệu và nghiệp vụ
    Client gửi request đến API (Controller).
    Controller gọi Application Service (manager, orchestrator) để xử lý use-case.
    Application Service dùng các interface của Domain để thao tác nghiệp vụ (repository, strategy, payment, notification, ...).
    Infrastructure cung cấp implement cho các interface đó (thông qua DI).
    Kết quả trả về cho Controller, Controller trả response cho client.
    Nếu có event nghiệp vụ (ví dụ: AppointmentCreated, PaymentProcessed), Application hoặc Infrastructure sẽ publish event lên RabbitMQ, các consumer sẽ xử lý tiếp (gửi mail, notification, ...).

4. Lợi ích khi dùng DDD
    Code rõ ràng, dễ hiểu, dễ bảo trì.
    Tách biệt nghiệp vụ và kỹ thuật.
    Dễ mở rộng, tích hợp, test.
    Đồng bộ với microservices, CQRS, event-driven.
    Giảm rủi ro khi thay đổi công nghệ, adapter, external service.