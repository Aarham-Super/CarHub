using CarHub.Models;

namespace CarHub.Services;

public sealed class CarCatalogService
{
    private static readonly IReadOnlyList<CarDetailViewModel> Catalog =
    [
        new(
            1,
            "Ferrari",
            "LaFerrari",
            "Ferrari LaFerrari",
            "/images/cars/Ferrari/LaFerrari/Amsterdam_Motor_Show_2023_-_110.jpg",
            [
                "/images/cars/Ferrari/LaFerrari/Amsterdam_Motor_Show_2023_-_110.jpg",
                "/images/cars/Ferrari/LaFerrari/Ferrari_AAA-F150_LaFerrari_Aperta_(No.48)_(23051409353).jpg",
                "/images/cars/Ferrari/LaFerrari/Ferrari_LaFerrari_Aperta_(2017)_(55080465027).jpg"
            ],
            "The LaFerrari is the halo car that anchors the CarHub Ferrari showcase with dramatic lines and track-focused energy.",
            "Hybrid hypercar",
            "V12 hybrid",
            "Rear-wheel drive",
            "217 mph",
            "2.6 sec",
            "Hero image by René Cortin on Wikimedia Commons, licensed CC BY-SA 4.0.",
            "https://commons.wikimedia.org/wiki/File:Amsterdam_Motor_Show_2023_-_110.jpg",
            [
                "René Cortin - Wikimedia Commons - CC BY-SA 4.0",
                "Unknown author - Wikimedia Commons - verify the individual file pages before publishing",
                "Unknown author - Wikimedia Commons - verify the individual file pages before publishing"
            ]
        ),
        new(
            2,
            "Ferrari",
            "Aperta",
            "Ferrari LaFerrari Aperta",
            "/images/cars/Ferrari/LaFerrari/Ferrari_LaFerrari_Aperta_(2017)_(55080465027).jpg",
            [
                "/images/cars/Ferrari/LaFerrari/Ferrari_LaFerrari_Aperta_(2017)_(55080465027).jpg",
                "/images/cars/Ferrari/LaFerrari/Ferrari_LaFerrari_Aperta_(2017)_(55080465037).jpg",
                "/images/cars/Ferrari/LaFerrari/Ferrari_LaFerrari_Aperta_(2017)_(55081624549).jpg"
            ],
            "An open-top Ferrari showcase page built around the same dramatic LaFerrari family assets already in the library.",
            "Open-top hypercar",
            "V12 hybrid",
            "Rear-wheel drive",
            "217 mph",
            "2.6 sec",
            "Ferrari LaFerrari Aperta images sourced from Wikimedia Commons files in the same LaFerrari collection.",
            "https://commons.wikimedia.org/wiki/Category:Ferrari_LaFerrari_Aperta",
            [
                "Wikimedia Commons - LaFerrari Aperta image set",
                "Wikimedia Commons - LaFerrari Aperta image set",
                "Wikimedia Commons - LaFerrari Aperta image set"
            ]
        ),
        new(
            3,
            "Lambo",
            "Huracan",
            "Lamborghini Huracan",
            "/images/cars/Lambo/Huracan/Lamborghini_Huracan_EVO_2.jpg",
            [
                "/images/cars/Lambo/Huracan/Lamborghini_Huracan_EVO_2.jpg",
                "/images/cars/Lambo/Huracan/Lamborghini_Huracan_Evo_Genf_2019_1Y7A5452.jpg",
                "/images/cars/Lambo/Huracan/Lamborghini_Huracan_Evo_Genf_2019_1Y7A5453.jpg"
            ],
            "A sharp, angular Lamborghini showcase with a compact footprint and unmistakable wedge silhouette.",
            "Supercar",
            "V10",
            "All-wheel drive",
            "202 mph",
            "2.9 sec",
            "Hero image by Calreyn88 on Wikimedia Commons, licensed CC BY-SA 4.0.",
            "https://commons.wikimedia.org/wiki/File:Lamborghini_Huracan_EVO_2.jpg",
            [
                "Calreyn88 - Wikimedia Commons - CC BY-SA 4.0",
                "Calreyn88 - Wikimedia Commons - CC BY-SA 4.0",
                "Calreyn88 - Wikimedia Commons - CC BY-SA 4.0"
            ]
        ),
        new(
            4,
            "Lambo",
            "Urus",
            "Lamborghini Urus",
            "/images/cars/Lambo/Urus/2023_Lamborghini_Urus_Performante_8.jpg",
            [
                "/images/cars/Lambo/Urus/2023_Lamborghini_Urus_Performante_8.jpg",
                "/images/cars/Lambo/Urus/2023_Lamborghini_Urus_Performante_9.jpg",
                "/images/cars/Lambo/Urus/Lamborghini_Urus_SE_001.jpg"
            ],
            "The Urus brings supercar drama to a practical SUV shell and gives the catalog a bold everyday hero.",
            "Super SUV",
            "Twin-turbo V8",
            "All-wheel drive",
            "190 mph",
            "3.3 sec",
            "Hero image by JustAnotherCarDesigner on Wikimedia Commons, licensed CC0.",
            "https://commons.wikimedia.org/wiki/File:Lamborghini_Urus_SE_001.jpg",
            [
                "JustAnotherCarDesigner - Wikimedia Commons - CC0",
                "JustAnotherCarDesigner - Wikimedia Commons - CC0",
                "JustAnotherCarDesigner - Wikimedia Commons - CC0"
            ]
        ),
        new(
            41,
            "Lambo",
            "UrusS",
            "Lamborghini Urus S",
            "/images/cars/Lambo/Urus/Lamborghini_Urus_S_(2024)_(54094039770).jpg",
            [
                "/images/cars/Lambo/Urus/Lamborghini_Urus_S_(2024)_(54094039770).jpg",
                "/images/cars/Lambo/Urus/Lamborghini_Urus_S_1X7A7428.jpg",
                "/images/cars/Lambo/Urus/Lamborghini_Urus_S_1X7A7435.jpg"
            ],
            "The Urus S brings a sharper daily-driver feel and gives the CarHub lineup a second Lamborghini SUV trim.",
            "Super SUV",
            "Twin-turbo V8",
            "All-wheel drive",
            "190 mph",
            "3.5 sec",
            "Hero image by Wikimedia Commons contributors, with source details listed per file on the credit page.",
            "https://commons.wikimedia.org/wiki/Category:Lamborghini_Urus",
            [
                "Wikimedia Commons - Urus S image set",
                "Wikimedia Commons - Urus S image set",
                "Wikimedia Commons - Urus S image set"
            ]
        ),
        new(
            42,
            "Lambo",
            "UrusSE",
            "Lamborghini Urus SE",
            "/images/cars/Lambo/Urus/Lamborghini_Urus_SE_001.jpg",
            [
                "/images/cars/Lambo/Urus/Lamborghini_Urus_SE_001.jpg",
                "/images/cars/Lambo/Urus/Lamborghini_Urus_SE_002.jpg",
                "/images/cars/Lambo/Urus/Lamborghini_Urus_SE_IMG_3908.jpg",
                "/images/cars/Lambo/Urus/Lamborghini_Urus_SE_Arancio_Egon_01.jpg"
            ],
            "The Urus SE adds the hybrid-flavored headline slot to the Lamborghini SUV section of the catalog.",
            "Hybrid super SUV",
            "Twin-turbo V8 hybrid",
            "All-wheel drive",
            "194 mph",
            "3.4 sec",
            "Hero image by JustAnotherCarDesigner on Wikimedia Commons, licensed CC0.",
            "https://commons.wikimedia.org/wiki/File:Lamborghini_Urus_SE_001.jpg",
            [
                "JustAnotherCarDesigner - Wikimedia Commons - CC0",
                "JustAnotherCarDesigner - Wikimedia Commons - CC0",
                "JustAnotherCarDesigner - Wikimedia Commons - CC0",
                "JustAnotherCarDesigner - Wikimedia Commons - CC0"
            ]
        ),
        new(
            5,
            "McLaren",
            "Artura",
            "McLaren Artura",
            "/images/cars/McLaren/Artura/McLaren_Artura.jpg",
            [
                "/images/cars/McLaren/Artura/McLaren_Artura.jpg",
                "/images/cars/McLaren/Artura/The_frontview_of_McLaren_ARTURA.jpg",
                "/images/cars/McLaren/Artura/The_rearview_of_McLaren_ARTURA.jpg"
            ],
            "A hybrid supercar showcase with a clean cockpit and a sharp, technical design language.",
            "Hybrid supercar",
            "V6 hybrid",
            "Rear-wheel drive",
            "205 mph",
            "3.0 sec",
            "Hero image by Tokumeigakarinoaoshima on Wikimedia Commons, licensed CC BY-SA 4.0.",
            "https://commons.wikimedia.org/wiki/File:The_frontview_of_McLaren_ARTURA.jpg",
            [
                "Tokumeigakarinoaoshima - Wikimedia Commons - CC BY-SA 4.0",
                "Tokumeigakarinoaoshima - Wikimedia Commons - CC BY-SA 4.0",
                "Tokumeigakarinoaoshima - Wikimedia Commons - CC BY-SA 4.0"
            ]
        ),
        new(
            6,
            "McLaren",
            "GTS",
            "McLaren GTS",
            "/images/cars/McLaren/GTS/McLaren_GTS.jpg",
            [
                "/images/cars/McLaren/GTS/McLaren_GTS.jpg",
                "/images/cars/McLaren/GTS/McLaren_GTS_(front_view)_at_Japan_Mobility_Show_Kansai_2025.jpg",
                "/images/cars/McLaren/GTS/McLaren_GTS_(front_three-quarter_view)_Japan_Mobility_Show_Kansai_2025.jpg"
            ],
            "A grand touring McLaren page that balances long-distance usability with serious visual presence.",
            "Grand tourer",
            "Twin-turbo V8",
            "Rear-wheel drive",
            "203 mph",
            "3.2 sec",
            "Hero image by Calreyn88 on Wikimedia Commons, licensed CC BY-SA 4.0.",
            "https://commons.wikimedia.org/wiki/File:McLaren_GTS.jpg",
            [
                "Calreyn88 - Wikimedia Commons - CC BY-SA 4.0",
                "Calreyn88 - Wikimedia Commons - CC BY-SA 4.0",
                "Calreyn88 - Wikimedia Commons - CC BY-SA 4.0"
            ]
        ),
        new(
            7,
            "McLaren",
            "LM",
            "McLaren LM",
            "/images/cars/McLaren/LM/McLaren_P1_LM_SCD_24.jpg",
            [
                "/images/cars/McLaren/LM/McLaren_P1_LM_SCD_24.jpg",
                "/images/cars/McLaren/LM/FoS20162016_0625_091900AA_(27624423280).jpg"
            ],
            "A rare, track-inspired McLaren page with a focused, collectible feel.",
            "Track-inspired special",
            "V8",
            "Rear-wheel drive",
            "217 mph",
            "2.8 sec",
            "Hero image by MrWalkr on Wikimedia Commons, licensed CC BY-SA 4.0.",
            "https://commons.wikimedia.org/wiki/File:McLaren_P1_LM_SCD_24.jpg",
            [
                "MrWalkr - Wikimedia Commons - CC BY-SA 4.0",
                "MrWalkr - Wikimedia Commons - CC BY-SA 4.0"
            ]
        ),
        new(
            8,
            "Toyota",
            "Corolla2025",
            "Toyota Corolla 2025",
            "/images/cars/Toyota/Corolla/2025/hero.jpg",
            [
                "/images/cars/Toyota/Corolla/2025/hero.jpg",
                "/images/cars/Toyota/Corolla/2025/gallery-1.jpg",
                "/images/cars/Toyota/Corolla/2025/hero.jpg"
            ],
            "A clean Toyota Corolla page built from a real Wikimedia Commons photo set inside the 2025 year folder.",
            "Compact sedan",
            "Hybrid-friendly four-cylinder",
            "Front-wheel drive",
            "118 mph",
            "8.2 sec",
            "Hero image by Trop86 and gallery image by crash71100, both from Wikimedia Commons.",
            "https://commons.wikimedia.org/wiki/Toyota_Corolla",
            [
                "Trop86 - Wikimedia Commons - CC0",
                "crash71100 - Wikimedia Commons - CC0",
                "Trop86 - Wikimedia Commons - CC0"
            ]
        ),
        new(
            9,
            "Toyota",
            "Corolla2026",
            "Toyota Corolla 2026",
            "/images/cars/Toyota/Corolla/2026/2025%20Toyota%20Corolla%20LE%20ice%20cap.jpg",
            [
                "/images/cars/Toyota/Corolla/2026/2025%20Toyota%20Corolla%20LE%20ice%20cap.jpg",
                "/images/cars/Toyota/Corolla/2026/Toyota%20Corolla%20Hybrid%20SEG%202025.jpg",
                "/images/cars/Toyota/Corolla/2026/Toyota%20Corolla%20Hybrid%20(2023)%20(53943519982).jpg"
            ],
            "A future-facing Corolla page with newer Commons exterior shots so the 2026 styling feels current instead of dated.",
            "Compact sedan",
            "Hybrid-friendly four-cylinder",
            "Front-wheel drive",
            "118 mph",
            "8.0 sec",
            "Hero image by Rachelhoots on Wikimedia Commons, gallery 1 by RL GNZLZ, and gallery 2 by Charles from Port Chester, New York.",
            "https://commons.wikimedia.org/wiki/File:2025_Toyota_Corolla_LE_ice_cap.jpg",
            [
                "Rachelhoots - Wikimedia Commons - CC BY-SA 4.0",
                "RL GNZLZ - Wikimedia Commons - CC BY-SA 2.0",
                "Charles from Port Chester, New York - Wikimedia Commons - CC BY 2.0"
            ]
        ),
        new(
            10,
            "Toyota",
            "Conquest2026",
            "Toyota Conquest 2026",
            "/images/cars/Toyota/Conquest/2026/hero.jpg",
            [
                "/images/cars/Toyota/Conquest/2026/hero.jpg",
                "/images/cars/Toyota/Conquest/2026/hero.jpg",
                "/images/cars/Toyota/Conquest/2026/hero.jpg"
            ],
            "A Toyota Conquest page using a dedicated model folder and year-based asset tree.",
            "Compact hatchback",
            "1.5L four-cylinder",
            "Front-wheel drive",
            "110 mph",
            "9.1 sec",
            "Hero image by Charles from Port Chester, New York on Wikimedia Commons.",
            "https://commons.wikimedia.org/wiki/Category:Toyota_Conquest",
            [
                "Charles - Wikimedia Commons - CC BY 2.0",
                "Charles - Wikimedia Commons - CC BY 2.0",
                "Charles - Wikimedia Commons - CC BY 2.0"
            ]
        ),
        new(
            11,
            "BMW",
            "M3",
            "BMW M3 2026",
            "/images/cars/BMW/M3/2026/hero.jpg",
            [
                "/images/cars/BMW/M3/2026/hero.jpg",
                "/images/cars/BMW/M3/2026/gallery-1.jpg",
                "/images/cars/BMW/M3/2026/hero.jpg"
            ],
            "A BMW M3 page using the same brand/model/year folder pattern as Toyota.",
            "Sport sedan",
            "Inline six turbo",
            "Rear-wheel drive",
            "180 mph",
            "3.8 sec",
            "Hero image by Calreyn88 and gallery image by FotoSleuth on Wikimedia Commons.",
            "https://commons.wikimedia.org/wiki/BMW_M3",
            [
                "Calreyn88 - Wikimedia Commons - CC0",
                "FotoSleuth - Wikimedia Commons - CC BY 2.0",
                "Calreyn88 - Wikimedia Commons - CC0"
            ]
        ),
        new(
            12,
            "BMW",
            "X5",
            "BMW X5 2026",
            "/images/cars/BMW/X5/2026/hero.jpg",
            [
                "/images/cars/BMW/X5/2026/hero.jpg",
                "/images/cars/BMW/X5/2026/gallery-1.jpg",
                "/images/cars/BMW/X5/2026/hero.jpg"
            ],
            "A BMW X5 page that rounds out the new brand folder with an SUV-style entry.",
            "Luxury SUV",
            "Turbocharged inline six",
            "All-wheel drive",
            "155 mph",
            "5.3 sec",
            "Hero image by Fields BMW Lakelands on Wikimedia Commons.",
            "https://commons.wikimedia.org/wiki/BMW_X5",
            [
                "Fields BMW Lakelands - Wikimedia Commons - CC BY 3.0",
                "Sonny doe - Wikimedia Commons - CC BY-SA 4.0",
                "Fields BMW Lakelands - Wikimedia Commons - CC BY 3.0"
            ]
        )
    ];

    public IReadOnlyList<CarDetailViewModel> GetAllCars() => Catalog;

    public IReadOnlyList<CarDetailViewModel> GetFeaturedCars(int count = 3) =>
        Catalog.Take(count).ToArray();

    public CarDetailViewModel? FindById(int id) =>
        Catalog.FirstOrDefault(car => car.Id == id);

    public CarDetailViewModel? Find(string brand, string model) =>
        Catalog.FirstOrDefault(car =>
            string.Equals(car.Brand, brand, StringComparison.OrdinalIgnoreCase)
            && string.Equals(car.Model, model, StringComparison.OrdinalIgnoreCase));
}
