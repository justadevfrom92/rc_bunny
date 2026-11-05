using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Seed;

public static partial class Seeder
{
    /// <summary>The ticket team (in-office and a 3rd-party company), tickets over the last few months, and website messages.</summary>
    private static async Task SeedRangerAsync(AppDbContext db, User admin, User csr, Role service, string password, string storageRoot)
    {
        const string vendor = "Lone Star Support";
        var riley = new User { Name = "Riley Vendor", Email = "riley@lonestarsupport.example", PasswordHash = password, Role = service, Company = vendor, Title = "Support Agent", AccessEndsOn = DateOnly.FromDateTime(DateTime.Today).AddYears(1) };
        var jamie = new User { Name = "Jamie Vendor", Email = "jamie@lonestarsupport.example", PasswordHash = password, Role = service, Company = vendor, Title = "Support Agent", AccessEndsOn = DateOnly.FromDateTime(DateTime.Today).AddYears(1) };
        db.Users.AddRange(riley, jamie);
        db.RangerAgents.AddRange(
            new RangerAgent { User = admin, Team = "office", Level = "manager", CreatedAt = DateTime.UtcNow.AddDays(-200) },
            new RangerAgent { User = csr, Team = "office", Level = "agent", AddedBy = admin, CreatedAt = DateTime.UtcNow.AddDays(-180) },
            new RangerAgent { User = riley, Team = "third_party", Level = "company_admin", AddedBy = admin, CreatedAt = DateTime.UtcNow.AddDays(-120) },
            new RangerAgent { User = jamie, Team = "third_party", Level = "agent", AddedBy = riley, CreatedAt = DateTime.UtcNow.AddDays(-100) });
        await db.SaveChangesAsync();

        var customers = await db.Customers.OrderBy(c => c.Id).Take(24).ToListAsync();
        var agents = new[] { csr, riley, jamie, admin };
        var asks = new (string Topic, string Subject, string Body)[]
        {
            ("billing", "Why is my bill higher this month?", "My bill went up about $40 from last month. Can you tell me why?"),
            ("plan", "When does my contract end?", "I'd like to know when my plan ends and what my options are."),
            ("service", "Moving next month", "I'm moving on the 15th. How do I move my service to the new place?"),
            ("billing", "Double charged?", "I see two payments on my card for the same bill."),
            ("account", "Can't log in to My Account", "The password reset email never shows up."),
            ("billing", "AutoPay didn't run", "AutoPay is on but my payment didn't go through this month."),
            ("plan", "Renewal options", "My plan is ending soon. What can I renew to?"),
            ("service", "Stop service at old address", "Please stop service at my old apartment on Friday."),
            ("rewards", "Where are my stars?", "I paid on time but didn't get my reward stars."),
            ("outage", "Power out on my street", "Our whole street has been out for two hours."),
            ("other", "Can I add my spouse to the account?", "I'd like my wife to be able to call about the account."),
            ("billing", "Payment arrangement", "Can I split this bill into two payments?"),
        };
        string[] channels = ["My Account", "My Account", "Email", "Phone"];
        string[] priorities = ["normal", "normal", "low", "high", "normal", "urgent"];
        var rnd = new Random(7);
        var tickets = new List<SupportTicket>();
        for (var i = 0; i < customers.Count; i++)
        {
            var c = customers[i];
            var ask = asks[i % asks.Length];
            var age = i < 8 ? i : 6 + i * 3; // the first few are recent and still open; the rest are spread over ~10 weeks
            var created = DateTime.UtcNow.AddDays(-age).AddHours(-rnd.Next(1, 9));
            var status = i switch { < 2 => "new", < 5 => "open", < 7 => "waiting", _ => i % 7 == 0 ? "closed" : "solved" };
            var owner = status == "new" ? null : agents[i % agents.Length];
            var channel = channels[i % channels.Length];
            var t = new SupportTicket
            {
                CustomerId = c.Id, Name = c.Name, Email = c.Email ?? "", Subject = ask.Subject, Topic = ask.Topic, Channel = channel, Status = status,
                Priority = priorities[i % priorities.Length], AssignedToId = owner?.Id, CreatedAt = created, UpdatedAt = created,
            };
            db.SupportTickets.Add(t);
            await db.SaveChangesAsync();
            tickets.Add(t);

            var via = channel switch { "Email" => "email", "Phone" => "phone", _ => "portal" };
            var first = new TicketMessage
            {
                TicketId = t.Id, Kind = "customer", Author = c.Name, Via = via, Body = ask.Body, CreatedAt = created,
                Direction = via == "phone" ? "in" : null, DurationSec = via == "phone" ? 300 + i * 20 : null, Contact = via == "email" ? c.Email : via == "phone" ? c.Phone : null,
            };
            db.TicketMessages.Add(first);
            if (owner == null) continue;

            var answered = created.AddHours(1 + i % 5);
            db.TicketMessages.Add(new TicketMessage { TicketId = t.Id, Kind = "system", Author = owner.Name, UserId = owner.Id, Body = $"{owner.Name} picked this up.", CreatedAt = answered.AddMinutes(-10) });
            db.TicketMessages.Add(new TicketMessage
            {
                TicketId = t.Id, Kind = "agent", Author = owner.Name, UserId = owner.Id, Via = via == "phone" ? "phone" : via, CreatedAt = answered,
                Direction = via == "phone" ? "out" : null, DurationSec = via == "phone" ? 420 : null, Contact = via == "email" ? c.Email : via == "phone" ? c.Phone : null,
                Body = via == "phone" ? "Called back and walked them through it. They're all set for now." : $"Hi {c.FirstName}, thanks for reaching out! I've looked at your account and here's what I found…",
            });
            t.FirstResponseAt = answered;
            t.UpdatedAt = answered;
            if (status is "waiting" or "solved" or "closed")
            {
                db.TicketMessages.Add(new TicketMessage { TicketId = t.Id, Kind = "customer", Author = c.Name, Body = "Thanks, that helps.", CreatedAt = answered.AddHours(3) });
                t.UpdatedAt = answered.AddHours(3);
            }
            if (status is "solved" or "closed")
            {
                t.SolvedAt = answered.AddHours(5);
                t.UpdatedAt = t.SolvedAt.Value;
                db.TicketMessages.Add(new TicketMessage { TicketId = t.Id, Kind = "system", Author = owner.Name, UserId = owner.Id, Body = $"{owner.Name} set the status to Solved.", CreatedAt = t.SolvedAt.Value });
                if (i % 5 != 4) // most customers rate us
                {
                    t.Rating = i % 6 == 3 ? "bad" : "good";
                    t.RatedAt = t.SolvedAt.Value.AddHours(6);
                    t.RatingComment = t.Rating == "bad" ? "Took too long to hear back." : i % 3 == 0 ? "Quick and friendly, thank you!" : null;
                }
            }
            if (i % 4 == 1)
                db.Notes.Add(new Note
                {
                    CustomerId = c.Id, UserId = owner.Id, Author = owner.Name, Category = "Ticket", Action = "Internal Note", Priority = "Low", SupportTicketId = t.Id,
                    Body = $"{t.Number}: Checked the last two bills, the usage went up with the heat.", CreatedAt = answered.AddMinutes(5),
                });
        }

        // A manager request, a manager who joined in, and a customer's photo of their meter
        var help = tickets[3];
        (help.ManagerRequestedAt, help.ManagerRequestedById, help.ManagerRequestNote) = (DateTime.UtcNow.AddHours(-2), help.AssignedToId, "Customer wants a refund over $100.");
        db.Notes.Add(new Note
        {
            CustomerId = help.CustomerId!.Value, UserId = help.AssignedToId, Author = (await db.Users.FindAsync(help.AssignedToId))!.Name, Category = "Ticket", Action = "Manager Request",
            Priority = "Medium", SupportTicketId = help.Id, Body = $"{help.Number}: Customer wants a refund over $100.", CreatedAt = DateTime.UtcNow.AddHours(-2),
        });
        db.TicketWatchers.Add(new TicketWatcher { TicketId = tickets[2].Id, UserId = admin.Id, JoinedAt = DateTime.UtcNow.AddHours(-5) });
        db.TicketMessages.Add(new TicketMessage { TicketId = tickets[2].Id, Kind = "system", Author = admin.Name, UserId = admin.Id, Body = $"{admin.Name} joined in.", CreatedAt = DateTime.UtcNow.AddHours(-5) });
        await db.SaveChangesAsync();

        var withFile = await db.TicketMessages.FirstAsync(m => m.TicketId == tickets[0].Id && m.Kind == "customer");
        using (var text = new MemoryStream("Meter reading taken 8:15 AM\nReading: 48213 kWh\nLast bill's reading: 47120 kWh\n"u8.ToArray()))
            db.TicketAttachments.Add(await Ranger.SaveFileAsync(storageRoot, withFile, "meter-reading.txt", text, null, true));

        // Website Contact Us messages
        db.ContactMessages.AddRange(
            new ContactMessage { Name = "Jordan Lee", Email = "jordan@example.com", Topic = "billing", Message = "Can I change my due date?", Ip = "203.0.113.24", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new ContactMessage { Name = "Sam Ortiz", Email = "sam@example.com", Phone = "(555) 555-0199", Topic = "signup", Message = "Moving to Dallas next month, which plan should I pick?", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new ContactMessage { Name = customers[5].Name, Email = customers[5].Email ?? "", Topic = "general", Message = "Is there a plan with free nights?", CreatedAt = DateTime.UtcNow.AddHours(-6) },
            new ContactMessage
            {
                Name = "Taylor Kim", Email = "taylor@example.com", Topic = "general", Message = "Do you serve Houston?", CreatedAt = DateTime.UtcNow.AddDays(-6),
                Reply = "Yes! We serve the CenterPoint area around Houston.", RepliedAt = DateTime.UtcNow.AddDays(-5), RepliedById = csr.Id, HandledAt = DateTime.UtcNow.AddDays(-5), HandledById = csr.Id,
                Resolution = "Resolved", ResolvedById = csr.Id, ResolvedAt = DateTime.UtcNow.AddDays(-5),
            });
        await db.SaveChangesAsync();
    }
}
