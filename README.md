  Payment Processor API

API REST para procesamiento asíncrono de pagos construida con **.NET 8**, diseñada con arquitectura orientada a **eventos**, con comunicación a un adquirente externo a través de **Polly** para resiliencia HTTP.

## Características

- ✅ **Procesamiento asíncrono** — Los pagos se encolan en un canal en memoria y se procesan en segundo plano.
- ✅ **Idempotencia** — Garantiza que una misma solicitud con el mismo `IdempotencyKey` no procese duplicados (ventana de 24h).
- ✅ **Resiliencia HTTP** — Políticas de **retry** (3 reintentos con exponential backoff) y **circuit breaker** (5 fallos, 30s de pausa) para la comunicación con el adquirente.
- ✅ **Event Sourcing** — Cada cambio de estado en una transacción se registra como un evento inmutable.
- ✅ **Logging estructurado** — Serilog con salida JSON para fácil integración con sistemas de monitoreo.
- ✅ **CQRS** — Separación de responsabilidades de lectura y escritura en capas de servicio y repositorio.
- ✅ **Documentación OpenAPI** — Swagger UI disponible en entorno de desarrollo.

## Stack Tecnológico

| Componente | Tecnología |
|---|---|
| **Runtime** | .NET 8 (ASP.NET Core) |
| **ORM** | Entity Framework Core 8 |
| **Base de datos** | Azure SQL Server |
| **Logging** | Serilog (formato JSON compacto) |
| **Resiliencia** | Polly (retry + circuit breaker) |
| **Cola en memoria** | `System.Threading.Channels` (Unbounded) |
| **Documentación** | Swashbuckle (Swagger / OpenAPI) |

## Arquitectura

La aplicación sigue un patrón de **procesamiento asíncrono basado en canales**:

1. El cliente HTTP envía una solicitud de pago a la API REST.
2. El controlador delega en `PaymentService`, que verifica idempotencia y persiste la transacción como `PENDING`.
3. El `TransactionId` se escribe en un canal en memoria (`System.Threading.Channels`).
4. Un `BackgroundService` (`PaymentWorkerService`) consume el canal e invoca la integración con el adquirente.
5. `AcquirerIntegrationService` actualiza el estado de la transacción (`PROCESSING` → `APPROVED` / `DECLINED` / `FAILED`) y registra eventos.
6. El cliente puede consultar el estado de la transacción mediante el endpoint `GET /payments/{id}`.

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          CLIENTE HTTP                                    │
│  POST /payments ─────────────────────────────────────────── GET /payments│
└────────────────────┬─────────────────────────────────────────┬───────────┘
                     │                                         │
                     ▼                                         │
┌────────────────────────────────────────────────────────┐     │
│              PAYMENT CONTROLLER                        │     │
│  ┌───────────────────┐   ┌──────────────────────────┐  │     │
│  │ PaymentValidator   │   │   IPaymentQueryService   │──┘     │
│  └───────────────────┘   └──────────────────────────┘        │
│  ┌───────────────────────────────────────────────────┐       │
│  │              IPaymentService                      │       │
│  │                                                   │       │
│  │  ┌──────────┐       ┌────────────────────────┐   │       │
│  │  │¿Idempot. │──Sí──→│ Retornar respuesta previa│   │       │
│  │  │  existe? │       └────────────────────────┘   │       │
│  │  │          │                                    │       │
│  │  │   No     │                                    │       │
│  │  │    ▼     │                                    │       │
│  │  │ ┌──────────────────────┐                     │       │
│  │  │ │Crear Transaction     │                     │       │
│  │  │ │(PENDING) +           │                     │       │
│  │  │ │IdempotencyRecord     │                     │       │
│  │  │ └──────────┬───────────┘                     │       │
│  │  │            ▼                                 │       │
│  │  │ ┌──────────────────────┐                     │       │
│  │  │ │  Commit BD           │                     │       │
│  │  │ └──────────┬───────────┘                     │       │
│  │  │            ▼                                 │       │
│  │  │ ┌──────────────────────┐                     │       │
│  │  │ │Escribir TransactionId│                     │       │
│  │  │ │ en IPaymentChannel   │                     │       │
│  │  │ └──────────────────────┘                     │       │
│  └──────────────────┬────────────────────────────────┘       │
└─────────────────────┼────────────────────────────────────────┘
                      │
                      ▼
┌──────────────────────────────────────────────────┐
│            IN-MEMORY CHANNEL                      │
│         (System.Threading.Channels)               │
└──────────────────────┬───────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────┐
│           BACKGROUND WORKER                       │
│          PaymentWorkerService                     │
│                                                   │
│  ┌──────────────────────────────────────────┐    │
│  │    IAcquirerIntegrationService            │    │
│  │                                           │    │
│  │  ┌─────────────────────────────┐          │    │
│  │  │ Actualizar STATUS → PROCESSING │        │    │
│  │  └─────────────┬───────────────┘          │    │
│  │                ▼                          │    │
│  │  ┌─────────────────────────────┐          │    │
│  │  │ POST /authorize (Adquirente)│          │    │
│  │  └─────────────┬───────────────┘          │    │
│  │                │                          │    │
│  │     ┌──────────┴──────────┐               │    │
│  │     ▼                     ▼               │    │
│  │  ┌──────────┐       ┌──────────┐          │    │
│  │  │ APROBADO │       │ RECHAZADO│          │    │
│  │  │ APPROVED │       │ DECLINED │          │    │
│  │  └──────────┘       └──────────┘          │    │
│  │         │               │                 │    │
│  │         ▼               ▼                 │    │
│  │  ┌───────────────────────────────────┐    │    │
│  │  │ Insertar TransactionEvent         │    │    │
│  │  └───────────────────────────────────┘    │    │
│  └──────────────────────────────────────────┘    │
└──────────────────────┬───────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────┐
│              AZURE SQL SERVER                     │
│         (Entity Framework Core)                   │
│                                                   │
│  ┌──────────────┐  ┌──────────────────┐          │
│  │ Transactions  │  │ TransactionEvents │         │
│  ├──────────────┤  ├──────────────────┤          │
│  │ - Id         │  │ - Id             │          │
│  │ - MerchantId │  │ - TransactionId  │          │
│  │ - Amount     │  │ - EventType      │          │
│  │ - Currency   │  │ - PreviousStatus │          │
│  │ - Status     │  │ - NewStatus      │          │
│  │ - CreatedAt  │  │ - CreatedAt      │          │
│  │ - UpdatedAt  │  └──────────────────┘          │
│  └──────────────┘                                │
│  ┌──────────────────┐                            │
│  │ IdempotencyRecords│                           │
│  ├──────────────────┤                            │
│  │ - Key (PK)       │                            │
│  │ - TransactionId  │                            │
│  │ - ResponseBody   │                            │
│  │ - CreatedAt      │                            │
│  └──────────────────┘                            │
└──────────────────────────────────────────────────┘

                     ┌──────────────────────┐
                     │     SERILOG (JSON)    │
                     │   Logging estructurado │
                     └──────────────────────┘
```

## Estados de Transacción

```
               ┌──────────┐
               │  PENDING │  ← POST /payments
               └────┬─────┘
                    │ Worker procesa
                    ▼
              ┌───────────┐
              │ PROCESSING │
              └─────┬─────┘
                    │
       ┌────────────┼────────────┐
       ▼            ▼            ▼
 ┌──────────┐ ┌──────────┐ ┌──────────┐
 │ APPROVED │ │ DECLINED │ │  FAILED  │
 └──────────┘ └──────────┘ └──────────┘
```

- **PENDING**: Transacción creada, esperando ser procesada.
- **PROCESSING**: El worker está comunicándose con el adquirente.
- **APPROVED**: El adquirente aprobó la transacción.
- **DECLINED**: El adquirente rechazó la transacción (por negocios o error 4xx).
- **FAILED**: Error de comunicación con el adquirente después de reintentos.

## Endpoints de la API

### `POST /payments` — Crear un pago

Crea una nueva transacción de pago y la encola para procesamiento asíncrono.

**Request body:**
```json
{
  "merchantId": "merchant-001",
  "amount": 15000,
  "currency": "CLP",
  "card": {
    "number": "4111111111111111",
    "expiry": "12/2028",
    "cvv": "123"
  },
  "idempotencyKey": "unique-key-123"
}
```

**Response (202 Accepted):**
```json
{
  "transactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "createdAt": "2026-06-16T23:00:00Z"
}
```

**Códigos de respuesta:**
| Código | Descripción |
|---|---|
| `202 Accepted` | Pago aceptado y en cola para procesamiento |
| `400 Bad Request` | Datos inválidos (validación de campos) |
| `409 Conflict` | `IdempotencyKey` ya fue procesada |

### `GET /payments/{id}` — Obtener detalle de transacción

Retorna el detalle completo de una transacción, incluyendo todos sus eventos de cambio de estado.

**Response (200 OK):**
```json
{
  "transactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "merchantId": "merchant-001",
  "amount": 15000,
  "currency": "CLP",
  "cardLastFour": "1111",
  "status": "Approved",
  "createdAt": "2026-06-16T23:00:00Z",
  "updatedAt": "2026-06-16T23:00:05Z",
  "events": [
    {
      "id": "event-id-1",
      "eventType": "PROCESSING",
      "previousStatus": null,
      "newStatus": "Processing",
      "createdAt": "2026-06-16T23:00:02Z"
    },
    {
      "id": "event-id-2",
      "eventType": "APPROVED",
      "previousStatus": "Processing",
      "newStatus": "Approved",
      "createdAt": "2026-06-16T23:00:05Z"
    }
  ]
}
```

**Códigos de respuesta:**
| Código | Descripción |
|---|---|
| `200 OK` | Transacción encontrada |
| `404 Not Found` | Transacción no encontrada |

### `GET /payments?merchantId={merchantId}&status={status}` — Listar transacciones

Obtiene transacciones filtradas por comercio y opcionalmente por estado.

**Parámetros query:**
| Parámetro | Tipo | Requerido | Descripción |
|---|---|---|---|
| `merchantId` | string | Sí | ID del comercio |
| `status` | string | No | Filtro por estado (`Pending`, `Processing`, `Approved`, `Declined`, `Failed`) |

**Response (200 OK):**
```json
{
  "items": [
    {
      "transactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "merchantId": "merchant-001",
      "amount": 15000,
      "currency": "CLP",
      "status": "Approved",
      "createdAt": "2026-06-16T23:00:00Z"
    }
  ],
  "totalCount": 1
}
```

## Casos de Éxito y Error

### `POST /payments` — Escenarios

```
Cliente envía POST /payments
         │
         ├── ✅ Caso exitoso (202 Accepted)
         │    └── Pago creado como PENDING y encolado para procesamiento async
         │
         ├── ❌ Error 400 — Validación de campos
         │    ├── MerchantId vacío
         │    ├── Amount <= 0
         │    ├── Currency inválido (no son 3 caracteres)
         │    ├── Card.Number con menos de 13 o más de 19 dígitos, o contiene letras
         │    ├── Card.Expiry en formato incorrecto (no coincide con MM/YYYY)
         │    ├── Card.Expiry con fecha vencida
         │    ├── Card.Cvv con menos de 3 o más de 4 dígitos, o contiene letras
         │    └── IdempotencyKey vacía
         │
         └── ❌ Error 409 — IdempotencyKey duplicada
              └── La misma IdempotencyKey ya fue procesada anteriormente
                  (el servidor retorna la respuesta original)
```

**Ejemplo de respuesta error 400:**
```json
{
  "errores": [
    "MerchantId es requerido",
    "Amount debe ser mayor que cero",
    "Card.Number debe tener entre 13 y 19 dígitos numéricos"
  ]
}
```

**Ejemplo de respuesta error 409:**
```json
{
  "error": "Idempotency key existe"
}
```

### `GET /payments/{id}` — Escenarios

```
Cliente solicita GET /payments/{id}
         │
         ├── ✅ Caso exitoso (200 OK)
         │    └── Transacción encontrada con detalle completo y eventos
         │
         └── ❌ Error 404 — Transacción no encontrada
              ├── El ID no es un GUID válido
              └── El GUID no existe en la base de datos
```

**Ejemplo de respuesta error 404:**
```json
{
  "error": "Transaccion no encontrada"
}
```

### `GET /payments?merchantId=...` — Escenarios

```
Cliente solicita GET /payments?merchantId=...&status=...
         │
         ├── ✅ Caso exitoso (200 OK) — Transacciones encontradas
         │    ├── Filtro por merchantId solamente
         │    ├── Filtro por merchantId + status válido (Pending/Processing/Approved/Declined/Failed)
         │    └── Lista vacía si no hay transacciones que coincidan
         │
         └── ❌ Sin resultados (200 OK con lista vacía)
              └── El merchantId no tiene transacciones, o el status no coincide con ninguna
```

### Procesamiento en Segundo Plano — Escenarios

Una vez que el pago es aceptado (202), el worker procesa la transacción de forma asíncrona. Estos son los posibles resultados al consultar el estado con `GET /payments/{id}`:

```
Transacción creada como PENDING
         │
         ▼
    Worker recoge del canal
         │
         ▼
    ┌─────────────────┐
    │ ✅ HAPPY PATH   │
    └─────────────────┘
    STATUS → PROCESSING
         │
    POST /authorize al adquirente (éxito)
         │
    STATUS → APPROVED
         │
    Se registra TransactionEvent (APPROVED)
    Resultado final: ✅ APPROVED


    ┌──────────────────────┐
    │ ❌ DECLINED          │
    └──────────────────────┘
    STATUS → PROCESSING
         │
    POST /authorize al adquirente (responde HTTP 4xx)
         │
    STATUS → DECLINED
         │
    Se registra TransactionEvent (DECLINED_BY_ACQUIRER)
    Resultado final: ❌ DECLINED


    ┌──────────────────────┐
    │ ❌ FAILED (error 5xx)│
    └──────────────────────┘
    STATUS → PROCESSING
         │
    POST /authorize al adquirente (responde HTTP 500)
         │
    Polly reintenta (3 veces con exponential backoff)
         │
    Sigue fallando → STATUS → FAILED
         │
    Se registra TransactionEvent (FAILED)
    Resultado final: ❌ FAILED


    ┌──────────────────────┐
    │ ❌ FAILED (timeout)  │
    └──────────────────────┘
    STATUS → PROCESSING
         │
    POST /authorize al adquirente (timeout)
         │
    Polly reintenta (3 veces)
         │
    Sigue fallando → STATUS → FAILED
         │
    Se registra TransactionEvent (FAILED)
    Resultado final: ❌ FAILED


    ┌──────────────────────────────────┐
    │ ⚠️ RECUPERACIÓN (edge case)     │
    └──────────────────────────────────┘
    Adquirente aprueba (HTTP 200), pero la BD falla al guardar
         │
    ┌─────────────────────────────────────┐
    │ Se reintenta actualizar en nuevo    │
    │ scope de base de datos              │
    └─────────────────────────────────────┘
         │
    ├── Éxito en reintento → RECOVERED (APPROVED)
    └── Falla en reintento → CRÍTICO (requiere intervención manual)
```

**Estados finales de una transacción:**

| Estado | Significado | Próximo paso |
|---|---|---|
| `Pending` | Creada, esperando en cola | Consultar nuevamente con GET /payments/{id} |
| `Processing` | El worker está procesando con el adquirente | Consultar nuevamente en unos segundos |
| `Approved` | ✅ Pago aprobado exitosamente | Finalizado |
| `Declined` | ❌ Pago rechazado por el adquirente | Finalizado |
| `Failed` | ❌ Error de comunicación con el adquirente | Finalizado (requiere reintento manual) |

## Estructura del Proyecto


```
PaymentProcessor/
├── Channels/                    # Canal en memoria (Producer/Consumer)
│   ├── IPaymentChannel.cs       #   Abstracción del canal
│   └── PaymentChannel.cs        #   Implementación con System.Threading.Channels
├── Controllers/                 # Controladores REST
│   └── PaymentController.cs     #   Endpoints: POST /payments, GET /payments/{id}, GET /payments
├── Data/                        # Capa de persistencia
│   ├── ApplicationDbContext.cs  #   DbContext de EF Core
│   ├── IPaymentRepository.cs    #   Abstracción repositorio escritura
│   ├── IPaymentQueryRepository.cs # Abstracción repositorio lectura (CQRS)
│   ├── PaymentRepository.cs     #   Implementación repositorio escritura
│   └── PaymentQueryRepository.cs #   Implementación repositorio lectura
├── DTOs/                        # Objetos de transferencia de datos
│   ├── CreatePaymentRequest.cs  #   Request de creación de pago
│   ├── CreatePaymentResponse.cs #   Response de creación
│   ├── PaymentDetailResponse.cs #   Detalle de transacción con eventos
│   └── PaymentListResponse.cs   #   Lista resumida de transacciones
├── Migrations/                  # Migraciones de EF Core
├── Models/                      # Entidades de dominio
│   ├── Enum/
│   │   └── TransactionStatusEnum.cs  # Pending, Processing, Approved, Declined, Failed
│   ├── Transaction.cs           #   Transacción de pago
│   ├── TransactionEvent.cs      #   Evento de cambio de estado
│   └── IdempotencyRecord.cs     #   Registro de idempotencia
├── Services/                    # Capa de negocio
│   ├── IPaymentService.cs       #   Abstracción servicio de pagos
│   ├── PaymentService.cs        #   Creación de pagos con idempotencia
│   ├── IPaymentQueryService.cs  #   Abstracción servicio de consultas
│   ├── PaymentQueryService.cs   #   Consultas de transacciones
│   ├── IAcquirerIntegrationService.cs # Abstracción integración adquirente
│   ├── AcquirerIntegrationService.cs  # Comunicación con adquirente + manejo de errores
│   └── PaymentRequestValidator.cs     # Validación de requests
├── Workers/                     # Procesamiento en segundo plano
│   └── PaymentWorkerService.cs  #   BackgroundService que consume el canal
├── Program.cs                   # Punto de entrada, DI, middleware, políticas Polly
├── appsettings.json             # Configuración (Azure SQL, Serilog, Acquirer)
└── ARCHITECTURE.md              # Documentación arquitectónica detallada
```

## Cómo Empezar

### Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Azure SQL Server (o SQL Server LocalDB para desarrollo)
- [Simulador de adquirente](https://github.com/tu-usuario/acquirer-simulator) — Repositorio aparte que simula un adquirente externo

### Configuración

1. Clonar el repositorio:
   ```bash
   git clone https://github.com/tu-usuario/payment-processor.git
   cd payment-processor/PaymentProcessor
   ```

2. Clonar y ejecutar el simulador de adquirente (puerto 5001):
   ```bash
   git clone https://github.com/tu-usuario/acquirer-simulator.git
   cd acquirer-simulator
   dotnet run --urls "http://localhost:5001"
   ```

3. Configurar la cadena de conexión en `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "ConnectionData": "Server=localhost;Database=PaymentProcessor;..."
     }
   }
   ```

4. Ejecutar la aplicación (las migraciones se aplican automáticamente al iniciar):
   ```bash
   dotnet run
   ```

5. Acceder a Swagger UI en `http://localhost:5000/swagger`

### Resiliencia

La comunicación con el adquirente incorpora dos políticas de resiliencia mediante Polly:

- **Retry Policy**: 3 reintentos con exponential backoff (100ms, 200ms, 400ms).
- **Circuit Breaker**: Se abre después de 5 fallos consecutivos, con una duración de 30 segundos.

Si después de los reintentos el adquirente no responde, la transacción se marca como `Failed`.
