# 🧪 Payment Processor API — Casos de Prueba

Este documento contiene ejemplos curl para todos los escenarios del sistema, incluyendo los diferentes comportamientos del adquirente simulado.

## ⚙️ Setup

```bash
# Terminal 1: Iniciar el adquirente simulado (puerto 5001)
cd c:\Repo\acquirer-simulator
dotnet run

# Terminal 2: Iniciar el procesador de pagos (puerto por defecto)
cd c:\Repo\testHaulmer
dotnet run
```

---

## 1. POST /payments — Crear un pago

### 🔵 Caso 1.1: Pago APROBADO (amount termina en 0)

El último dígito `0` → el adquirente responde `200 OK { status: "APPROVED" }`.

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-approved-001"
  }'
```

**Respuesta (202 Accepted):**
```json
{
  "transactionId": "a1b2c3d4-...",
  "status": "Pending",
  "createdAt": "2026-06-24T15:30:00Z"
}
```

**Transición de estados:** `PENDING` → `PROCESSING` → `APPROVED`

---

### 🔵 Caso 1.2: Pago APROBADO (amount termina en 4-9)

Último dígito `4` → el adquirente responde con `APPROVED` (default).

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15004,
    "currency": "CLP",
    "card": {
      "number": "5500000000000004",
      "expiry": "06/2027",
      "cvv": "456"
    },
    "idempotencyKey": "key-approved-004"
  }'
```

**Transición de estados:** `PENDING` → `PROCESSING` → `APPROVED`

---

### 🔴 Caso 1.3: Pago RECHAZADO por negocio (amount termina en 1)

Último dígito `1` → el adquirente responde `400 Bad Request` → `DECLINED`.

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15001,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-declined-001"
  }'
```

**Transición de estados:** `PENDING` → `PROCESSING` → `DECLINED`

---

### 🟠 Caso 1.4: Pago FALLIDO por timeout (amount termina en 2)

Último dígito `2` → el adquirente demora 30s y responde `504` → agota 3 reintentos Polly → `FAILED`.

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15002,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-timeout-002"
  }'
```

**Transición de estados:** `PENDING` → `PROCESSING` → `FAILED`
**Reintentos:** 3, con backoff exponencial (100ms → 200ms → 400ms)

---

### 🟠 Caso 1.5: Pago FALLIDO por error de servidor (amount termina en 3)

Último dígito `3` → el adquirente responde `503` → agota 3 reintentos Polly → `FAILED`.

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15003,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-error-003"
  }'
```

**Transición de estados:** `PENDING` → `PROCESSING` → `FAILED`

---

## 2. POST /payments — Validaciones (400 Bad Request)

### 🔴 Caso 2.1: Falta merchantId

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-err-001"
  }'
```

**Respuesta (400):**
```json
{ "errores": ["MerchantId es requerido"] }
```

---

### 🔴 Caso 2.2: Amount <= 0

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 0,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-err-002"
  }'
```

**Respuesta (400):**
```json
{ "errores": ["Amount debe ser mayor que cero"] }
```

---

### 🔴 Caso 2.3: Currency inválida

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "XX",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-err-003"
  }'
```

**Respuesta (400):**
```json
{ "errores": ["Currency debe ser un código válido (ej: CLP, USD)"] }
```

---

### 🔴 Caso 2.4: Número de tarjeta muy corto

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "123",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-err-004"
  }'
```

**Respuesta (400):**
```json
{ "errores": ["Card.Number debe tener entre 13 y 19 dígitos numéricos"] }
```

---

### 🔴 Caso 2.5: Formato de expiry inválido

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "13/2028",
      "cvv": "123"
    },
    "idempotencyKey": "key-err-005"
  }'
```

**Respuesta (400):**
```json
{ "errores": ["Card.Expiry debe tener formato MM/YYYY"] }
```

---

### 🔴 Caso 2.6: Tarjeta vencida

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "01/2020",
      "cvv": "123"
    },
    "idempotencyKey": "key-err-006"
  }'
```

**Respuesta (400):**
```json
{ "errores": ["La tarjeta está vencida"] }
```

---

### 🔴 Caso 2.7: CVV inválido

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "12"
    },
    "idempotencyKey": "key-err-007"
  }'
```

**Respuesta (400):**
```json
{ "errores": ["Card.Cvv debe tener 3 o 4 dígitos numéricos"] }
```

---

### 🔴 Caso 2.8: Falta idempotencyKey

```bash
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": ""
  }'
```

**Respuesta (400):**
```json
{ "errores": ["IdempotencyKey es requerida"] }
```

---

## 3. Idempotencia

### 🟢 Caso 3.1: Misma idempotencyKey → misma respuesta (sin duplicar)

Envía el mismo request dos veces con la misma `idempotencyKey`. Ambos retornan `202` con el mismo `transactionId`.

```bash
# Primer intento
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "idem-dup-001"
  }'

# Segundo intento (misma key) → misma respuesta
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "merchant-001",
    "amount": 15000,
    "currency": "CLP",
    "card": {
      "number": "4111111111111111",
      "expiry": "12/2028",
      "cvv": "123"
    },
    "idempotencyKey": "idem-dup-001"
  }'
```

**Ambas respuestas (202):**
```json
{
  "transactionId": "<mismo-id>",
  "status": "Pending",
  "createdAt": "<misma-fecha>"
}
```

---

### 🟡 Caso 3.2: Idempotencia con TTL de 24 horas

Un registro de idempotencia creado hace más de 24 horas **no se encuentra** y se crea una transacción nueva.

```bash
# Este caso se prueba esperando 24h o manipulando la BD directamente:
# UPDATE IdempotencyRecords SET CreatedAt = DATEADD(HOUR, -25, CreatedAt)
# WHERE [Key] = 'key-expirada';
```

---

## 4. GET /payments/{id} — Consultar estado de una transacción

### 🟢 Caso 4.1: Consultar transacción existente

Usa el `transactionId` retornado por cualquier `POST /payments`.

```bash
curl -X GET http://localhost:5000/payments/a1b2c3d4-5678-90ab-cdef-1234567890ab
```

**Respuesta (200 OK) — pago APROBADO:**
```json
{
  "transactionId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",
  "merchantId": "merchant-001",
  "amount": 15000,
  "currency": "CLP",
  "cardLastFour": "1111",
  "status": "Approved",
  "createdAt": "2026-06-24T15:30:00Z",
  "updatedAt": "2026-06-24T15:30:05Z",
  "events": [
    {
      "id": "e1e1e1e1-...",
      "eventType": "PENDING",
      "previousStatus": null,
      "newStatus": "Pending",
      "createdAt": "2026-06-24T15:30:00Z"
    },
    {
      "id": "e2e2e2e2-...",
      "eventType": "PROCESSING",
      "previousStatus": "Pending",
      "newStatus": "Processing",
      "createdAt": "2026-06-24T15:30:01Z"
    },
    {
      "id": "e3e3e3e3-...",
      "eventType": "APPROVED",
      "previousStatus": "Processing",
      "newStatus": "Approved",
      "createdAt": "2026-06-24T15:30:05Z"
    }
  ]
}
```

---

### 🔴 Caso 4.2: Transacción no encontrada

```bash
curl -X GET http://localhost:5000/payments/00000000-0000-0000-0000-000000000000
```

**Respuesta (404):**
```json
{ "error": "Transaccion no encontrada" }
```

---

## 5. GET /payments — Listar historial

### 🟢 Caso 5.1: Filtrar por merchantId

```bash
curl -X GET "http://localhost:5000/payments?merchantId=merchant-001"
```

**Respuesta (200):**
```json
{
  "items": [
    {
      "transactionId": "a1b2c3d4-...",
      "merchantId": "merchant-001",
      "amount": 15000,
      "currency": "CLP",
      "status": "Approved",
      "createdAt": "2026-06-24T15:30:00Z"
    }
  ],
  "totalCount": 1
}
```

---

### 🟢 Caso 5.2: Filtrar por merchantId + status

```bash
curl -X GET "http://localhost:5000/payments?merchantId=merchant-001&status=Approved"
```

**Estados válidos para filtrar:** `Pending`, `Processing`, `Approved`, `Declined`, `Failed`

---

## 6. Ciclo de Vida Completo — Resumen

```
┌──────────┐
│ PENDING  │  ← POST /payments (202 Accepted)
└────┬─────┘
     │ Worker recoge del canal
     ▼
┌───────────┐
│ PROCESSING│  ← Llama al adquirente
└─────┬─────┘
      │
      ├── amount termina en 0,4-9 ──→ APPROVED  ✅
      ├── amount termina en 1     ──→ DECLINED  ❌ (rechazo negocio)
      ├── amount termina en 2     ──→ FAILED    ⚠️ (timeout, 3 reintentos)
      └── amount termina en 3     ──→ FAILED    ⚠️ (error servidor, 3 reintentos)
```

## Tabla Rápida: Último Dígito del Monto

| Último dígito | Respuesta Adquirente | Estado Final | Reintentos |
|---|---|---|---|
| 0 | 200 OK `APPROVED` | `Approved` | 0 |
| 1 | 400 Bad Request | `Declined` | 0 |
| 2 | Timeout 30s → 504 | `Failed` | 3 |
| 3 | 503 Service Unavailable | `Failed` | 3 |
| 4 | 200 OK `APPROVED` | `Approved` | 0 |
| 5 | 200 OK `APPROVED` | `Approved` | 0 |
| 6 | 200 OK `APPROVED` | `Approved` | 0 |
| 7 | 200 OK `APPROVED` | `Approved` | 0 |
| 8 | 200 OK `APPROVED` | `Approved` | 0 |
| 9 | 200 OK `APPROVED` | `Approved` | 0 |
