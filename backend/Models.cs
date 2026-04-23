namespace TruckingApi;

public record DatabaseOptions(string ConnectionString);

public record SchemaChunk(string Text, float[] Embedding);
public record ChatRequest(string Message);

public record CreateLoadRequest(
    string LoadNumber, string ShipDate, string Origin, string Destination,
    string Description, decimal LineHaulRate, decimal FscRate,
    decimal TarpRate, decimal ExtraFee, string Terms,
    string Status, string BillToName, string BillToAddress,
    string ConsigneeName, string ConsigneeAddress, int? DriverId
);

public record UpdateStatusRequest(string Status);

public record GeneratePayRequest(
    int DriverId, string PayPeriodStart, string PayPeriodEnd,
    decimal InsuranceDeduction, decimal WorkersCompDeduction
);

public record GenerateInvoiceRequest(int LoadId, string? InvoiceDate);

public record LoginRequest(string Email, string Password, string ExpectedRole);

public record CreateEmployeeRequest(
    string FirstName, string LastName, string Email, string Phone, string Password
);

public record UpdateEmployeeRequest(
    string FirstName, string LastName, string Email, string Phone, string? Password
);

public record CreateTruckerRequest(
    string FirstName, string LastName, string Email, string Phone, string Password,
    string UnitNumber, string Address, decimal CommissionRate
);

public record UpdateTruckerRequest(
    string FirstName, string LastName, string Email, string Phone, string? Password,
    string UnitNumber, string Address, decimal CommissionRate
);
