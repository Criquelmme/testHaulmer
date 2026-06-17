using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentProcessor.DTOs;
using PaymentProcessor.Services;

namespace PaymentProcessor.Controllers;

// SOLID (SRP): Responsabilidad única → manejar requests HTTP relacionados a pagos.
// SOLID (DIP): Depende de abstracciones (IPaymentService, IPaymentQueryService).
[ApiController]
[Route("payments")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentQueryService _queryService;
    private readonly PaymentRequestValidator _validator;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        IPaymentQueryService queryService,
        PaymentRequestValidator validator,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _queryService = queryService;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Crea una nueva transacción de pago.
    /// </summary>
    /// <param name="request">Datos del pago.</param>
    /// <returns>202 Accepted con transaction_id, status PENDING y created_at.</returns>
    /// <response code="202">Pago aceptado y en cola para procesamiento.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="409">Conflicto de idempotencia (ya procesado).</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreatePaymentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        string metodo = nameof(CreatePayment);

        var errores = _validator.Validate(request);
        if (errores.Any())
        {
            return BadRequest(new { errores });
        }

        try
        {
            CreatePaymentResponse response = await _paymentService.CreatePayment(request);


            _logger.LogInformation("Pago {TransactionId} creado con estado {Estado} método {Metodo} evento {Evento}",
                response.TransactionId, response.Status, metodo, "PAYMENT_CREATED");

            return Accepted(response);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("PK__Idempote") == true)
        {
            _logger.LogWarning(ex, "Idempotency duplicado para key {IdempotencyKey} método {Metodo} evento {Evento}",
                request.IdempotencyKey, metodo, "IDEMPOTENCY_CONFLICT");
            return Conflict(new { error = "Idempotency key existe" });
        }
    }

    /// <summary>
    /// Obtiene el detalle de una transacción por su ID.
    /// </summary>
    /// <param name="id">ID de la transacción.</param>
    /// <returns>Detalle de la transacción con eventos.</returns>
    /// <response code="200">Transacción encontrada.</response>
    /// <response code="404">Transacción no encontrada.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PaymentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        PaymentDetailResponse? result = await _queryService.GetById(id);

        if (result == null)
        {
            return NotFound(new { error = "Transaccion no encontrada" });
        }
        return Ok(result);
    }

    /// <summary>
    /// Obtiene transacciones filtradas por merchant_id y opcionalmente por status.
    /// </summary>
    /// <param name="merchantId">ID del comercio (requerido).</param>
    /// <param name="status">Filtro opcional por estado.</param>
    /// <returns>Lista de transacciones filtradas.</returns>
    /// <response code="200">Lista obtenida exitosamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaymentListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFiltered(
        [FromQuery] string merchantId,
        [FromQuery] string? status)
    {
        PaymentListResponse result = await _queryService.GetFiltered(merchantId, status);
        return Ok(result);
    }
}
