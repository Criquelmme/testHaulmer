# Payment Processor API — Arquitectura

## Principios SOLID Aplicados

| Principio | Aplicación |
|-----------|-----------|
| **SRP** (Single Responsibility) | Cada clase tiene una única responsabilidad: `PaymentService` crea pagos, `AcquirerIntegrationService` se comunica con el adquirente, `PaymentWorkerService` consume el canal. |
| **OCP** (Open/Closed) | Las políticas de resiliencia (Polly) se extienden desde `Program.cs` sin modificar el código de negocio. |
| **LSP** (Liskov Substitution) | Las implementaciones concretas `PaymentChannel`, `PaymentService`, etc. pueden reemplazarse sin alterar los consumidores. |
| **ISP** (Interface Segregation) | Interfaces pequeñas y específicas: `IPaymentService` (creación), `IPaymentQueryService` (consulta), `IAcquirerIntegrationService` (adquirente), `IPaymentChannel` (cola). |
| **DIP** (Dependency Inversion) | Todas las dependencias apuntan a abstracciones (interfaces), no a implementaciones concretas. Registro explícito en `Program.cs`. |

## Diagrama de Flujo

```
Cliente HTTP
    │
    ▼
PaymentController.CreatePayment()
    │
    ▼
IPaymentService.CreatePaymentAsync()
    ├── Verifica idempotencia (24h TTL)
    ├── Guarda Transaction como PENDING
    ├── Guarda IdempotencyRecord
    ├── Commit transacción BD
    └── Escribe TransactionId en IPaymentChannel (Producer)
    │
    ▼
PaymentWorkerService (BackgroundService) ← lee del canal (Consumer)
    │
    ▼
IAcquirerIntegrationService.ProcessWithAcquirerAsync()
    ├── Actualiza status → PROCESSING
    ├── POST al adquirente (via HttpClient con Polly)
    ├── Actualiza status → APPROVED / DECLINED / FAILED
    └── Inserta TransactionEvent
```

## Estructura de Carpetas

```
PaymentProcessor/
├── Channels/          # IPaymentChannel, PaymentChannel (cola en memoria)
├── Controllers/       # PaymentController (endpoints REST)
├── Data/              # ApplicationDbContext, configuraciones EF Core
├── DTOs/              # CreatePaymentRequest/Response, PaymentDetailResponse, PaymentListResponse
├── Models/            # Transaction, TransactionEvent, IdempotencyRecord, TransactionStatus
├── Services/          # IPaymentService/PaymentService, IAcquirerIntegrationService/..., IPaymentQueryService/...
├── Workers/           # PaymentWorkerService (BackgroundService)
├── Program.cs         # Configuración de Serilog, DbContext, Polly, DI, Swagger
└── appsettings.json   # Cadena de conexión Azure SQL, Serilog
```

## Stack Técnico

- **.NET 8** — Runtime
- **Entity Framework Core 8** — ORM con Azure SQL Server
- **Serilog** — Logging estructurado en JSON
- **Polly** — Resiliencia HTTP (3 reintentos + exponential backoff + circuit breaker)
- **System.Threading.Channels** — Cola en memoria (Unbounded)
- **Swashbuckle** — Swagger / OpenAPI
