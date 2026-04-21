namespace TruckingApi;

public record DatabaseOptions(string ConnectionString);

public record SchemaChunk(string Text, float[] Embedding);
public record ChatRequest(string Message);

public record CreateLoadRequest(
    string LoadNumber, string ShipDate, string Origin, string Destination,
    string Description, decimal LineHaulRate, decimal FscRate, string Terms,
    string Status, string BillToName, string BillToAddress,
    string ConsigneeName, string ConsigneeAddress, int? DriverId
);

public record UpdateStatusRequest(string Status);
