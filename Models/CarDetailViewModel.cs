namespace CarHub.Models;

public sealed record CarDetailViewModel(
    int Id,
    string Brand,
    string Model,
    string DisplayName,
    string HeroImage,
    IReadOnlyList<string> GalleryImages,
    string Description,
    string Segment,
    string Powertrain,
    string Drivetrain,
    string TopSpeed,
    string Acceleration,
    string CreditLine,
    string CreditUrl,
    IReadOnlyList<string> GalleryCredits
)
{
    public string RouteBrand => Brand;

    public string RouteModel => Model;
}
