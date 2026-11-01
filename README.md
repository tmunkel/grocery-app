# Grocery App

Mobile-first web app for a **shopping list** and a **home grocery inventory**. Built with Blazor Web App (interactive server, .NET 10), EF Core and PostgreSQL. Product data comes from the [product inventory service](../product-inventory-service) over REST.

## Features

- One central text box: type an EAN, use a USB/Bluetooth barcode scanner (keyboard mode, ending with Enter), or type a product name
- 📷 Camera barcode scanning on phones (native `BarcodeDetector`, with a ZXing fallback for browsers such as iOS Safari)
- Mode toggle **Shopping / Inventory**. A found product is added to the active list, or its quantity is increased if it is already there
- +/- quantity controls. Decreasing to zero removes the item
- Shopping list: tick items as bought, then **Move bought to inventory** (quantities are merged) or **Remove bought**
- Unknown EAN → **New product** dialog, prefilled from Open Food Facts (name, brand, size, category, image)
- Take or upload a product photo. The browser resizes it before upload
- Product images are proxied through the app (`/product-images/{id}`), so phones only need to reach the app

## Run locally

Prerequisites: .NET 10 SDK, Docker, and a running [product inventory service](../product-inventory-service) on <http://localhost:5100>.

```powershell
docker compose up -d                      # PostgreSQL on localhost:5434
dotnet dev-certs https --trust            # once
dotnet run --project src/GroceryApp
```

Open <https://localhost:7200>. Migrations are applied on startup.

### Use from a phone

Camera access requires HTTPS. Run with the `lan` profile so the app listens on all interfaces:

```powershell
dotnet run --project src/GroceryApp --launch-profile lan
```

Then open `https://<your-pc-ip>:7200` on the phone and accept the development certificate warning. For permanent use, set up a proper certificate or a reverse proxy. The app has no authentication, so only expose it on a trusted network.

## Configuration (`appsettings.json`)

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:Grocery` | `Host=localhost;Port=5434;...` | PostgreSQL connection |
| `ProductInventory:BaseUrl` | `http://localhost:5100/` | Product inventory service URL |
| `Database:MigrateOnStartup` | `true` | Apply EF Core migrations on startup |

## Tests

```powershell
dotnet test
```

The tests use bUnit component tests and `ListService` tests against in-memory SQLite. They do not need Docker.
