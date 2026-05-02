# CarHub

CarHub is an ASP.NET 10 showcase site for car browsing, local account signup/login, OAuth sign-in, and a self-contained 3D configurator.

## Features

- ASP.NET 10 MVC app
- Tailwind-based layout
- Car catalog pages for Ferrari, Lamborghini, McLaren, Toyota, and BMW
- Local email/password accounts with password reset codes
- Google and GitHub OAuth sign-in
- Stripe webhook endpoint support
- Custom 403, 404, and error pages
- Apache 2.0 license

## Local setup

1. Open the solution in Visual Studio or run it with `dotnet run`.
2. Add your own values to `appsettings.json` for:
   - Google OAuth
   - GitHub OAuth
   - Stripe keys
   - Database connection strings
3. Start the app and open the home page.

## Notes

- `appsettings*.json` is ignored by Git so secrets stay local.
- Local account reset codes expire after 15 minutes and are single-use.
- The 3D viewer works without external model files.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE).
