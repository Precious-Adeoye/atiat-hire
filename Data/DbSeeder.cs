using AtiatHire.Infrastructure;
using AtiatHire.Models;
using AtiatHire.Services;
using Microsoft.EntityFrameworkCore;

namespace AtiatHire.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, bool seedDemoRequests)
    {
        if (!await db.Vehicles.AnyAsync())
        {
            db.Vehicles.AddRange(
                new Vehicle { Name = "Toyota Prado",       PlateNumber = "LND-101AA", Type = VehicleType.SUV,    Capacity = 7,  Status = VehicleStatus.Available },
                new Vehicle { Name = "Toyota Land Cruiser", PlateNumber = "LND-102AB", Type = VehicleType.SUV,   Capacity = 7,  Status = VehicleStatus.Available },
                new Vehicle { Name = "Toyota Camry",       PlateNumber = "LND-201AC", Type = VehicleType.Sedan,  Capacity = 4,  Status = VehicleStatus.OnHire },
                new Vehicle { Name = "Toyota Corolla",     PlateNumber = "LND-202AD", Type = VehicleType.Sedan,  Capacity = 4,  Status = VehicleStatus.Available },
                new Vehicle { Name = "Toyota Hiace Bus",   PlateNumber = "LND-301AE", Type = VehicleType.Bus,    Capacity = 14, Status = VehicleStatus.Maintenance, Notes = "Brake service" },
                new Vehicle { Name = "Toyota Sienna",      PlateNumber = "LND-401AF", Type = VehicleType.Van,    Capacity = 7,  Status = VehicleStatus.Available },
                new Vehicle { Name = "Lexus LX 570",       PlateNumber = "LND-501AG", Type = VehicleType.Luxury, Capacity = 5,  Status = VehicleStatus.Available });
            await db.SaveChangesAsync();
        }

        if (!seedDemoRequests || await db.HireRequests.AnyAsync())
            return;

        await SeedDemoRequestsAsync(db);
    }

    // ---------------------------------------------------------------------------------------------
    // Demo data: ~60 requests over the last six months so the dashboard and insights have something
    // to show. All names, phone numbers and emails are fictional. Disable with Seed:DemoData=false.
    // ---------------------------------------------------------------------------------------------
    private static async Task SeedDemoRequestsAsync(AppDbContext db)
    {
        var rng = new Random(20260918);
        var vehicles = await db.Vehicles.ToListAsync();
        var nowUtc = DateTime.UtcNow;
        var nowLagos = TimeHelper.LagosNow;

        var names = new[]
        {
            "Amaka Obi", "Tunde Bakare", "Ngozi Eze", "Ibrahim Musa", "Funke Adeyemi", "Chidi Okafor",
            "Zainab Bello", "Emeka Nwosu", "Bisi Coker", "Yusuf Danjuma", "Kemi Balogun", "Segun Ogunleye"
        };
        var pickups = new[]
        {
            "Murtala Muhammed Airport, Ikeja", "Victoria Island", "Lekki Phase 1", "Ikoyi",
            "Ikeja GRA", "Apapa", "Maryland", "Ajah"
        };
        var destinations = new[]
        {
            "Victoria Island", "Lekki Phase 1", "Ikoyi", "Ikeja GRA", "Ibadan", "Abeokuta",
            "Epe", "Murtala Muhammed Airport, Ikeja", "Badagry"
        };
        var durations = new[] { 4, 8, 10, 24, 48, 72 };
        var path = new[]
        {
            RequestStatus.Pending, RequestStatus.Reviewed, RequestStatus.Quoted,
            RequestStatus.Confirmed, RequestStatus.Completed
        };

        var created = new List<HireRequest>();

        for (var i = 0; i < 60; i++)
        {
            var createdAt = nowUtc.AddDays(-rng.Next(1, 180)).AddMinutes(-rng.Next(0, 1440));
            var type = PickType(rng);
            var pickup = TimeHelper.ToLagos(createdAt).Date
                .AddDays(rng.Next(1, 14))
                .AddHours(6 + rng.Next(0, 14));

            var pickupInPast = pickup < nowLagos;
            bool cancelled;
            int steps;
            if (pickupInPast)
            {
                cancelled = rng.NextDouble() < 0.3;
                steps = cancelled ? rng.Next(0, 3) : 4;
            }
            else
            {
                cancelled = rng.NextDouble() < 0.15;
                steps = cancelled ? rng.Next(0, 3) : rng.Next(0, 4);
            }

            var request = new HireRequest
            {
                ReferenceNumber = "TMP-" + Guid.NewGuid().ToString("N"),
                CustomerName = names[rng.Next(names.Length)],
                Phone = "0803" + (1000000 + i).ToString(),
                Email = $"customer{i + 1}@example.com",
                PickupLocation = pickups[rng.Next(pickups.Length)],
                Destination = destinations[rng.Next(destinations.Length)],
                PickupDateTime = pickup,
                DurationHours = durations[rng.Next(durations.Length)],
                Passengers = PassengersFor(type, rng),
                PreferredVehicleType = type,
                DriverRequirement = rng.NextDouble() < 0.9 ? DriverRequirement.WithDriver : DriverRequirement.SelfDrive,
                Purpose = (TripPurpose)rng.Next(Enum.GetValues<TripPurpose>().Length),
                CreatedAt = createdAt
            };

            // Build a realistic status history, never later than "now".
            var t = createdAt;
            request.History.Add(new RequestStatusChange
            {
                FromStatus = null, ToStatus = RequestStatus.Pending,
                ChangedAt = t, ChangedBy = "Customer", Note = "Request submitted online"
            });

            var current = RequestStatus.Pending;
            for (var s = 1; s <= steps; s++)
            {
                t = Min(t.AddMinutes(s == 1 ? rng.Next(10, 600) : rng.Next(60, 1500)), nowUtc);
                request.History.Add(new RequestStatusChange
                {
                    FromStatus = current, ToStatus = path[s], ChangedAt = t, ChangedBy = "Operations Desk"
                });
                if (s == 1) request.FirstRespondedAt = t;
                current = path[s];
            }

            if (cancelled)
            {
                t = Min(t.AddMinutes(rng.Next(30, 900)), nowUtc);
                request.History.Add(new RequestStatusChange
                {
                    FromStatus = current, ToStatus = RequestStatus.Cancelled, ChangedAt = t, ChangedBy = "Operations Desk"
                });
                current = RequestStatus.Cancelled;
                request.ClosedAt = t;
            }
            else if (current == RequestStatus.Completed)
            {
                request.ClosedAt = t;
            }

            request.Status = current;

            if (steps >= 2)
                request.QuotedAmount = rng.Next(20, 400) * 1000m;

            if (steps >= 3 && !cancelled)
            {
                var matching = vehicles.Where(v => v.Type == type).ToList();
                if (matching.Count > 0)
                    request.AssignedVehicleId = matching[rng.Next(matching.Count)].Id;
            }

            created.Add(request);
        }

        // Insert oldest first so reference numbers increase with time.
        db.HireRequests.AddRange(created.OrderBy(r => r.CreatedAt));
        await db.SaveChangesAsync();

        foreach (var r in created)
            r.ReferenceNumber = HireRequestService.FormatReference(r.Id);
        await db.SaveChangesAsync();
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static VehicleType PickType(Random rng)
    {
        var roll = rng.Next(100);
        return roll switch
        {
            < 40 => VehicleType.SUV,
            < 65 => VehicleType.Sedan,
            < 80 => VehicleType.Bus,
            < 90 => VehicleType.Van,
            _ => VehicleType.Luxury
        };
    }

    private static int PassengersFor(VehicleType type, Random rng) => type switch
    {
        VehicleType.Sedan => rng.Next(1, 4),
        VehicleType.SUV => rng.Next(2, 7),
        VehicleType.Bus => rng.Next(8, 15),
        VehicleType.Van => rng.Next(4, 8),
        _ => rng.Next(1, 4)
    };
}
