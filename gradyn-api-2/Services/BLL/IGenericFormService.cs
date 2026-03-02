namespace gradyn_api_2.Services.BLL;

public interface IGenericFormService
{
    /// <summary>
    /// Appends a CSV row for the given form key. The CSV header is read from the existing file on each submission.
    /// The submitted dictionary keys should match header names. Missing keys are written as empty cells.
    /// Extra keys are ignored.
    /// </summary>
    Task SubmitAsync(
        string formKey,
        IReadOnlyDictionary<string, string?> fields,
        CancellationToken cancellationToken = default);
}