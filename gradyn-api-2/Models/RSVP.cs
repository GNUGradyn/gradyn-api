namespace gradyn_api_2.Models;

public class RSVP
{
    public required string[] GuestNames { get; init; }
    public required string ContactInformation { get; init; }
    public required string AdditionalInformation { get; init; }
    public required bool ContactAboutHotels { get; init; }
}