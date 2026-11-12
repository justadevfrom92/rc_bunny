using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Seed;

public static partial class Seeder
{
    /// <summary>
    /// Deputy (ebunny's DeputySeeder): open-weight models (hosted and downloaded), the agents on them, 30 days of
    /// (fictional) text and chat conversations, five live ones, and a week of automation runs.
    /// </summary>
    private static async Task SeedDeputyAsync(AppDbContext db)
    {
        var random = new Random(7);
        var now = DateTime.UtcNow;
        var m = new Dictionary<string, AiModel>();
        foreach (var (k, name, provider, id, source, size, quant, ctx, status) in new (string, string, string, string, string, decimal?, string?, int, string)[]
        {
            // Hosted open-weight models, through the OpenAI-compatible open models API
            ("kimi", "Kimi K2 Instruct", "hosted", "moonshotai/Kimi-K2-Instruct", "Moonshot AI, open weights on Hugging Face", null, null, 131072, "available"),
            ("qwen235", "Qwen3 235B A22B", "hosted", "Qwen/Qwen3-235B-A22B-Instruct-2507", "Alibaba Qwen, open weights on Hugging Face", null, null, 262144, "available"),
            ("deepseek", "DeepSeek V3", "hosted", "deepseek-ai/DeepSeek-V3", "DeepSeek, open weights on Hugging Face", null, null, 131072, "available"),
            // Downloaded to the local model server (Ollama)
            ("qwen8", "Qwen3 8B", "local", "qwen3:8b", "ollama pull qwen3:8b", 5.2m, "Q4_K_M", 40960, "available"),
            ("qwen30", "Qwen3 30B A3B", "local", "qwen3:30b-a3b", "ollama pull qwen3:30b-a3b", 19.0m, "Q4_K_M", 40960, "downloading"),
            ("gptoss", "gpt-oss 20B", "local", "gpt-oss:20b", "ollama pull gpt-oss:20b", 14.0m, "MXFP4", 131072, "available"),
            ("llama", "Llama 3.1 8B", "local", "llama3.1:8b", "ollama pull llama3.1:8b", 4.9m, "Q4_K_M", 131072, "available"),
            ("mistral", "Mistral 7B", "local", "mistral:7b", "ollama pull mistral:7b", 4.1m, "Q4_0", 32768, "available"),
            ("billing", "Billing Assistant (fine-tuned Qwen3 8B)", "local", "billing-assistant:8b", "/models/billing-assistant-8b-q4_k_m.gguf, fine-tuned on sample billing questions", 5.2m, "Q4_K_M", 40960, "available"),
        })
        {
            m[k] = new AiModel { Name = name, Provider = provider, ModelId = id, Source = source, SizeGb = size, Quantization = quant, ContextWindow = ctx, Status = status, CheckedAt = now.AddHours(-random.Next(2, 31)) };
            db.AiModels.Add(m[k]);
        }
        await db.SaveChangesAsync();

        const string Base = "You are a customer service agent for a Texas retail electricity provider. Be friendly, brief and accurate. Never guess account details; if you are unsure or the customer is upset, hand the conversation to a person.";
        var agents = new Dictionary<string, AiAgent>();
        foreach (var (k, name, desc, model, fallback, temperature, max, active, schedule) in new (string, string, string, string, string?, decimal, int, bool, string?)[]
        {
            // Talk to customers: texts and the My Account chat bubble
            ("sms", "Text Responder", "Replies to customer texts", "qwen8", "llama", 0.3m, 600, true, null),
            ("chat", "My Account Chat", "Answers the chat bubble in My Account", "qwen235", "qwen8", 0.4m, 1500, true, null),
            // Automations: run on a schedule to fix issues in the background (Sheriff → Cron Automations)
            ("queues", "Queue Triage", "Moves new orders into the right exception queue", "billing", "qwen8", 0.0m, 500, true, "*/15 * * * *"),
            ("enrollments", "Enrollment Fixer", "Retries enrollments stuck waiting on ERCOT and fixes address mismatches", "kimi", "qwen30", 0.0m, 800, true, "0 * * * *"),
            ("payments", "Payment Matcher", "Matches unapplied payments to the right account", "billing", "deepseek", 0.0m, 500, true, "30 6 * * *"),
            ("surveys", "Survey Tagger", "Tags survey comments by topic and mood", "mistral", "gptoss", 0.1m, 300, true, "0 2 * * *"),
            ("reports", "Report Summarizer", "Writes a short summary of finished Walker reports", "gptoss", "qwen30", 0.3m, 1500, false, "*/30 * * * *"),
        })
        {
            agents[k] = new AiAgent
            {
                Name = name, Activity = k, Description = desc, AiModelId = m[model].Id, FallbackModelId = fallback == null ? null : m[fallback].Id, Temperature = temperature,
                MaxTokens = max, Active = active, Schedule = schedule,
                SystemPrompt = $"{Base}\n\nYour job: {char.ToLower(desc[0])}{desc[1..]}.{(k == "chat" ? " If the customer asks for a person, or you can't help, answer with HANDOFF on its own line." : "")}",
            };
            db.AiAgents.Add(agents[k]);
        }
        await db.SaveChangesAsync();

        // Conversations: texts and My Account chats, with the issue, transcript, outcome and who took over
        var issues = new Dictionary<string, (string Q, string A)>
        {
            ["High bill"] = ("Why is my bill so high this month?", "Your usage went up about 30% with the heat, and the rate is the same. I can set up a payment arrangement if that helps."),
            ["Payment arrangement"] = ("Can I split this bill into two payments?", "Yes. I can split it over your next two bills; the first half is due on the usual date."),
            ["Moving"] = ("I am moving next month, what do I do?", "I can start service at your new address and stop it at the old one on the same day. What is the new address?"),
            ["Outage"] = ("My power is out.", "Outages are handled by your utility. I am texting you their outage number and will check back when power is restored."),
            ["Renewal"] = ("My contract is ending, what are my options?", "You have three renewal plans available. The 12-month plan keeps your rate within half a cent."),
            ["AutoPay"] = ("How do I turn on AutoPay?", "I can turn it on now with the card ending in your saved payment method. You will also earn AutoPay stars each month."),
            ["Rewards"] = ("How many stars do I have?", "You have stars ready to redeem for a bill credit or a gift card. Want me to redeem them?"),
            ["Deposit"] = ("Why do I need a deposit?", "The credit check asked for a deposit. You can pay it now, or enroll in AutoPay and paperless billing to lower it."),
        };
        var topics = issues.Keys.ToArray();
        // Not the My Account test sign-ins, so their chat starts empty
        var customers = (await db.Customers.Where(c => c.Phone != null && c.PasswordHash == null).ToListAsync()).OrderBy(_ => random.Next()).Take(60).ToList();
        var csrs = (await db.Users.Include(u => u.Role).Where(u => u.Active).ToListAsync()).Where(u => u.PermissionList().Contains("corral")).ToList();
        if (customers.Count == 0 || csrs.Count == 0) return;
        foreach (var (k, count) in new[] { ("sms", 90), ("chat", 80) })
        {
            var agent = agents[k];
            for (var i = 0; i < count; i++)
            {
                var topic = topics[random.Next(topics.Length)];
                var (q, a) = issues[topic];
                var roll = random.Next(1, 101);
                var outcome = roll <= 70 ? "resolved" : roll <= 92 ? "handed_off" : "abandoned";
                // A few answered by the fallback model
                var model = random.Next(1, 13) == 1 && agent.FallbackModelId != null ? agent.FallbackModelId : agent.AiModelId;
                var c = customers[random.Next(customers.Count)];
                var lines = new List<string> { $"Bot: Hi {c.FirstName}, thanks for reaching out. How can I help?", $"Customer: {q}", "Bot: Let me look at your account.", $"Bot: {a}" };
                User? handed = null;
                if (outcome == "handed_off")
                {
                    handed = csrs[random.Next(csrs.Count)];
                    lines.AddRange(["Customer: I would rather talk to a person.", "Bot: Of course. I am getting a person for you now and passing along what we covered.",
                        $"System: {handed.Name} took over.", $"{AiConversation.SpeakerFor(handed)}: Hi {c.FirstName}, I have your account open. I can help with that.", "Customer: Thanks, that fixed it."]);
                }
                else if (outcome == "resolved") lines.AddRange(["Customer: That works, thank you.", "Bot: You are welcome. Have a great day!"]);
                var start = now.Date.AddDays(-random.Next(0, 30)).AddHours(random.Next(7, 22)).AddMinutes(random.Next(0, 60));
                var latest = now.AddMinutes(-random.Next(90, 301));
                if (start > latest) start = latest;
                var sec = random.Next(20, 601);
                db.AiConversations.Add(new AiConversation
                {
                    AiAgentId = agent.Id, AiModelId = model, CustomerId = c.Id, Channel = k, Topic = topic, Outcome = outcome, Status = "closed", HandedToId = handed?.Id,
                    Transcript = string.Join("\n", lines), DurationSec = sec, InputTokens = random.Next(900, 4201), OutputTokens = random.Next(120, 901),
                    StartedAt = start, LastMessageAt = start.AddSeconds(sec), CreatedAt = start,
                });
            }
        }
        // Live right now: two waiting for a person, one a person is answering, two with the bot
        foreach (var (k, status, person, topic, ago) in new (string, string, User?, string, int)[]
            { ("sms", "waiting", null, "Payment arrangement", 4), ("chat", "waiting", null, "High bill", 2), ("chat", "agent", csrs[0], "Moving", 6), ("sms", "bot", null, "Outage", 1), ("chat", "bot", null, "Rewards", 3) })
        {
            var c = customers[random.Next(customers.Count)];
            var (q, a) = issues[topic];
            var lines = new List<string> { $"Bot: Hi {c.FirstName}, thanks for reaching out. How can I help?", $"Customer: {q}", $"Bot: {a}" };
            if (status != "bot") lines.AddRange(["Customer: Can I talk to a person please?", "Bot: Of course. I am getting a person for you now. They will see everything we covered."]);
            if (person != null) lines.AddRange([$"System: {person.Name} took over.", $"{AiConversation.SpeakerFor(person)}: Hi {c.FirstName}, I can help with the move. What day do you need service at the new address?", "Customer: The 28th."]);
            var start = now.AddMinutes(-(ago + 4));
            db.AiConversations.Add(new AiConversation
            {
                AiAgentId = agents[k].Id, AiModelId = agents[k].AiModelId, CustomerId = c.Id, Channel = k, Topic = topic, Outcome = status == "bot" ? "resolved" : "handed_off",
                Status = status, HandedToId = person?.Id, Transcript = string.Join("\n", lines), DurationSec = 240, InputTokens = random.Next(400, 1201), OutputTokens = random.Next(80, 301),
                StartedAt = start, LastMessageAt = now.AddMinutes(-ago), CreatedAt = start,
            });
        }

        // Automations: one run per schedule tick over the last week (capped), each with what it checked and fixed
        foreach (var (k, perDay, text) in new[]
        {
            ("queues", 96, "Checked {c} new orders: {f} moved to the right queue, {g} left in Unassigned for a person."),
            ("enrollments", 24, "Looked at {c} enrollments waiting on ERCOT: retried {f}, {g} need a person (address does not match the ESIID)."),
            ("payments", 1, "Matched {f} of {c} unapplied payments to accounts; {g} need a person."),
            ("surveys", 1, "Tagged {f} of {c} new survey comments; {g} were unclear."),
        })
        {
            var agent = agents[k];
            var every = 1440 / perDay;
            for (var i = 0; i < Math.Min(7 * perDay, 120); i++)
            {
                var checkedN = random.Next(k == "payments" ? 4 : 1, (k == "queues" ? 12 : 40) + 1);
                var failed = random.Next(1, 31) == 1;
                var fixedN = failed ? 0 : random.Next((int)Math.Floor(checkedN * .6), checkedN + 1);
                var flagged = failed ? 0 : checkedN - fixedN;
                var at = now.AddMinutes(-(i + 1) * every);
                db.AiAutomationRuns.Add(new AiAutomationRun
                {
                    AiAgentId = agent.Id, AiModelId = failed ? null : agent.AiModelId, Status = failed ? "failed" : "ok", Checked = failed ? 0 : checkedN, Fixed = fixedN, Flagged = flagged,
                    Summary = failed ? "The model did not answer (timed out after 120 seconds). Nothing was changed." : text.Replace("{c}", checkedN.ToString()).Replace("{f}", fixedN.ToString()).Replace("{g}", flagged.ToString()),
                    DurationSec = failed ? 120 : random.Next(3, 91), InputTokens = failed ? 0 : checkedN * random.Next(300, 701), OutputTokens = failed ? 0 : checkedN * random.Next(20, 81),
                    StartedAt = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, DateTimeKind.Utc),
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
