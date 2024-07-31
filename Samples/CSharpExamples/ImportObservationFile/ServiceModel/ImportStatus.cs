
namespace ImportObservationFile.ServiceModel
{
    /// <summary>
    ///ImportProcessorTransactionStatus will have these values:
    /// PENDING,
    /// IN_PROGRESS,
    /// COMPLETED,
    /// COMPLETED_WITH_ERRORS,
    /// BAD_REQUEST,
    /// SYSTEM_ERROR
    /// </summary>
    /// <param name="Id"></param>
    /// <param name="ImportProcessorTransactionStatus"></param>
    public record ImportStatus(string Id, string ImportProcessorTransactionStatus);
}
