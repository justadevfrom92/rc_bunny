using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

/// <summary>
/// The account actions in Corral's "-- Status --" and "-- Customer Service --" menus (Corral.Actions). Each one changes the
/// account and returns a message, or a page to send the agent to; every action (except deleting the order) leaves a note.
/// Actions that need an outside service (Experian, Stripe, email, texts) say honestly that nothing was sent when it isn't set up.
/// </summary>
public class AccountActions(AppDbContext db, Customer c, string who, IConfiguration config)
{
    public record Result(string Message, string? GoTo = null);

    public async Task<Result> RunAsync(Corral.AccountAction action, Dictionary<string, string> input)
    {
        string S(string k) => input.GetValueOrDefault(k)?.Trim() ?? "";
        decimal M(string k) => decimal.Parse(S(k));
        int N(string k) => int.Parse(S(k));
        DateOnly D(string k) => DateOnly.Parse(S(k));

        var result = action.Key switch
        {
            "credit-run" => await CreditRun(),
            "credit-max-deposit" => await CreditMaxDeposit(),
            "credit-waive-deposit" => await CreditWaiveDeposit(S("reason")),
            "send-to-market" => await SendToMarket(D("date")),
            "temp-disconnect" => await Disconnect(D("date"), temporary: true),
            "disconnect" => await Disconnect(D("date"), temporary: false),
            "reconnect" => await Reconnect(),
            "move-out" => await MoveOut(D("date")),
            "cancel-order" => await CancelOrder(S("reason")),
            "delete-order" => await DeleteOrder(),
            "winback" => await Winback(),
            "clear-exceptions" => await ClearExceptions(),
            "repair-plan" => await RepairPlan(),
            "undo-last-plan" => await UndoLastPlan(),
            "add-order" => new Result("Opened a new order for this premise.", $"/corral/orders/new?esiid={Uri.EscapeDataString(c.Esiid ?? "")}"),
            "make-payment" => await MakePayment(M("amount"), N("method")),
            "add-bill-credit" => AddBillCredit(M("amount"), S("description")),
            "balance-adjustment" => BalanceAdjustment(M("amount"), S("description")),
            "payment-arrangement" => await PaymentPlan("Payment Arrangement", "VIP - Payment Arrangement", "QueueCartVipPaymentarrangement", N("installments"), D("date")),
            "deferred-payment-plan" => await PaymentPlan("Deferred Payment Plan", "VIP - Deferred Payment Plan", "QueueCartVipDeferredpayment", N("installments"), D("date")),
            "transfer-payments" => await TransferPayment(N("payment"), S("account")),
            "transfer-stars" => await TransferStars(N("stars"), S("account")),
            "balanced-billing" => await BalancedBilling(),
            "transfer-service" => await TransferService(S("street"), S("city"), S("zip"), S("esiid"), D("date")),
            "change-plan" => new Result("Opened Renew / Change Plan.", $"/corral/orders/new?renew={c.Account}"),
            "spend-stars" => new Result("Opened Spend Stars.", $"/corral/customers/{c.Id}/stars"),
            "reset-login" => new Result("MyAccount login reset requested. " + Contact("Email", "MyAccount - Password Reset")),
            "add-authorized-user" => AddAuthorizedUser(S("name"), S("phone")),
            "send-email" => new Result(Contact("Email", S("template"), S("message"))),
            "send-text" => new Result(Contact("SMS", "Agent text", S("message"))),
            "test-meter" => MeterRequest("Meter Test Request", "QueueCartMeterTest", "Meter test requested."),
            "reread" => MeterRequest("Meter Re-Read Request", "QueueCartMeterRead", "Meter re-read requested."),
            "request-usage" => MeterRequest("Usage Request", "QueueCartUsagerequest", "Usage requested from the utility.", "867_03"),
            _ => throw new InvalidOperationException("Unknown action."),
        };

        if (action.Key != "delete-order")
            db.Notes.Add(new Note { CustomerId = c.Id, Author = who, Category = "Order", Action = action.Label, Priority = "Low", Body = $"{action.Label}: {result.Message}" });
        await db.SaveChangesAsync();
        return result;
    }

    // ---- helpers -----------------------------------------------------------------------------------------------

    /// <summary>Is an outside service set up? (Its settings are under Integrations:Name:… in appsettings.)</summary>
    private bool Configured(string integration) => config.GetSection($"Integrations:{integration}").GetChildren().Any(s => !string.IsNullOrEmpty(s.Value));

    /// <summary>Leave the account's current order-flow queue and enter a new one.</summary>
    private async Task MoveToQueue(string queue, string? note = null)
    {
        var current = await db.QueueLogs.Where(q => q.CustomerId == c.Id && q.ExitedAt == null && q.Queue.StartsWith("QueueCart")
            && !q.Queue.StartsWith("QueueCartException") && !q.Queue.StartsWith("QueueCartVip")).ToListAsync();
        foreach (var q in current) q.ExitedAt = DateTime.UtcNow;
        EnterQueue(queue, note);
    }

    private void EnterQueue(string queue, string? note = null) =>
        db.QueueLogs.Add(new QueueLog { CustomerId = c.Id, Queue = queue, Note = note, Who = who });

    private void Ercot(string type, string label, DateOnly? date = null, string? esiid = null) =>
        db.ErcotTransactions.Add(new ErcotTransaction
        {
            CustomerId = c.Id, Esiid = esiid ?? c.Esiid ?? "", TransDate = DateOnly.FromDateTime(DateTime.Today), Purpose = "request",
            ScheduledOn = date, TransType = type, Label = label,
        });

    private void Flag(string flag) => db.CustomerFlags.Add(new CustomerFlag { CustomerId = c.Id, Flag = flag, Author = who });

    /// <summary>Log an email or text. It's only "queued" when the service is set up; otherwise it says it wasn't sent.</summary>
    private string Contact(string channel, string template, string? body = null)
    {
        var integration = channel == "SMS" ? "Twilio" : "Smtp";
        var ready = Configured(integration);
        db.ContactLogs.Add(new ContactLog
        {
            CustomerId = c.Id, Channel = channel, Template = template, Body = body, Phone = channel == "SMS" ? c.Phone : null,
            Status = ready ? "queued" : "not sent", Who = who,
        });
        return ready ? $"{channel} \"{template}\" queued." : $"{channel} \"{template}\" logged but not sent: {integration} isn't set up.";
    }

    private async Task<Customer> OtherAccount(string account)
    {
        var other = await db.Customers.FirstOrDefaultAsync(x => x.Account == account);
        if (other is null || other.Id == c.Id) throw new ActionProblem("Enter a different, existing account number.");
        return other;
    }

    // ---- Status ----------------------------------------------------------------------------------------------

    private Task<Result> CreditRun()
    {
        var ok = Configured("Experian");
        db.ApiLogs.Add(new ApiLog
        {
            CustomerId = c.Id, Api = "Experian", Action = "credit.check", Status = ok ? "not built" : "skipped",
            Request = $"{{\"account\":\"{c.Account}\",\"zip\":\"{c.Zip}\",\"ssn\":\"•••• hidden\"}}",
            Response = ok ? "The credit check client hasn't been built yet: nothing was sent." : "Experian isn't set up: nothing was sent.",
        });
        return Task.FromResult(new Result(ok ? "Experian settings are there, but the credit check client hasn't been built yet." : "Credit not run: Experian isn't set up."));
    }

    private async Task<Result> CreditMaxDeposit()
    {
        var amount = c.Type == "Residential" ? 400m : 1000m;
        db.LedgerEntries.Add(new LedgerEntry { CustomerId = c.Id, Kind = "debit", Amount = amount, Description = "Deposit", RequestedBy = who });
        c.DepositDue = amount;
        c.Status = "Pending - Deposit Due";
        await MoveToQueue("QueueCartFulfilledDeposit");
        return new Result($"Deposit of {amount:C} required.");
    }

    private async Task<Result> CreditWaiveDeposit(string reason)
    {
        var deposits = await db.LedgerEntries.Where(e => e.CustomerId == c.Id && e.Kind == "debit" && e.Description == "Deposit" && e.Status == "pending").ToListAsync();
        foreach (var d in deposits) d.Status = "deleted";
        c.DepositDue = 0;
        c.Status = "Pending - No Deposit Due";
        return new Result($"Deposit waived{(deposits.Count > 0 ? $" ({deposits.Count} pending deposit removed)" : "")}. Reason: {reason}");
    }

    private async Task<Result> SendToMarket(DateOnly date)
    {
        var (type, label) = c.MoveSwitch == "move" ? ("814_16", "MoveIn") : ("814_01", "Switch");
        Ercot(type, label, date);
        c.Status = "Pending - Utility Not Answered";
        c.RequestedStart = date;
        await MoveToQueue("QueueCartFulfilledRequested");
        return new Result($"Enrollment ({type} {label}) sent to ERCOT for {date:yyyy-MM-dd}.");
    }

    private async Task<Result> Disconnect(DateOnly date, bool temporary)
    {
        Ercot("650_01", temporary ? "Disconnect for Non-Pay (temporary)" : "Disconnect for Non-Pay", date);
        c.Status = "Pending - Disconnect";
        await MoveToQueue(temporary ? "QueueCartDisconnectSent" : "QueueCartDisconnect");
        return new Result($"{(temporary ? "Temporary disconnect" : "Disconnect")} requested for {date:yyyy-MM-dd}.");
    }

    private async Task<Result> Reconnect()
    {
        Ercot("650_01", "Reconnect", DateOnly.FromDateTime(DateTime.Today));
        c.Status = "Good - On Flow";
        await MoveToQueue("QueueCartReconnectSent");
        return new Result("Reconnect requested.");
    }

    private async Task<Result> MoveOut(DateOnly date)
    {
        Ercot("814_24", "Move Out", date);
        foreach (var a in await db.ServiceAddresses.Where(a => a.CustomerId == c.Id && a.ServiceEnd == null).ToListAsync()) a.ServiceEnd = date;
        foreach (var t in await db.PlanTerms.Where(t => t.CustomerId == c.Id && t.Status == "current").ToListAsync()) t.ContractEnd = date;
        c.Status = "Moved Out";
        await MoveToQueue("QueueCartMvo");
        return new Result($"Move out scheduled for {date:yyyy-MM-dd}.");
    }

    private async Task<Result> CancelOrder(string reason)
    {
        if (await db.ErcotTransactions.AnyAsync(t => t.CustomerId == c.Id)) Ercot("814_08", "Cancel");
        c.Status = "Cancelled";
        Flag("Order has been cancelled manually");
        await MoveToQueue("QueueCartCancel", reason);
        return new Result($"Order cancelled. Reason: {reason}");
    }

    private async Task<Result> DeleteOrder()
    {
        await Accounts.DeleteAsync(db, c);
        return new Result($"Account {c.Account} deleted.", "/corral/customers");
    }

    private async Task<Result> Winback()
    {
        await MoveToQueue("QueueCartWinback");
        return new Result("Added to the winback queue. " + Contact("Email", "Winback Offer"));
    }

    private async Task<Result> ClearExceptions()
    {
        var was = c.Exception;
        c.Exception = null;
        foreach (var q in await db.QueueLogs.Where(q => q.CustomerId == c.Id && q.ExitedAt == null && q.Queue.StartsWith("QueueCartException")).ToListAsync()) q.ExitedAt = DateTime.UtcNow;
        foreach (var f in await db.CustomerFlags.Where(f => f.CustomerId == c.Id && f.RemovedAt == null && f.Flag.StartsWith("Exception:")).ToListAsync()) f.RemovedAt = DateTime.UtcNow;
        return new Result(was != null ? $"Cleared exception: {was}." : "No exceptions to clear.");
    }

    private async Task<Result> RepairPlan()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var terms = await db.PlanTerms.Where(t => t.CustomerId == c.Id).OrderBy(t => t.ContractStart).ToListAsync();
        var current = terms.LastOrDefault(t => t.ContractStart <= today);
        foreach (var t in terms)
            t.Status = t == current ? "current" : t.ContractStart <= today ? "ended" : "not started";
        if (current?.PlanId is { } planId && planId != c.PlanId) (c.PlanId, c.RateClass) = (planId, current.RateClass);
        return new Result(current != null ? $"Current plan set to {current.RateClass}." : "No plan term has started yet.");
    }

    private async Task<Result> UndoLastPlan()
    {
        var last = await db.PlanTerms.Where(t => t.CustomerId == c.Id && t.Status == "not started").OrderByDescending(t => t.OrderedAt).ThenByDescending(t => t.Id).FirstOrDefaultAsync();
        if (last is null) return new Result("No pending plan to undo.");
        db.PlanTerms.Remove(last);
        return new Result($"Removed pending plan {last.RateClass}.");
    }

    // ---- Customer service ------------------------------------------------------------------------------------

    private async Task<Result> MakePayment(decimal amount, int methodId)
    {
        if (!await db.PaymentMethods.AnyAsync(m => m.Id == methodId && m.CustomerId == c.Id && m.RemovedAt == null)) throw new ActionProblem("Pick one of the account's payment methods.");
        var ok = Configured("Stripe");
        db.Payments.Add(new Payment
        {
            CustomerId = c.Id, Reference = "PAY-" + Code(), PaidOn = DateOnly.FromDateTime(DateTime.Today), PaidTime = TimeOnly.FromDateTime(DateTime.Now),
            Amount = amount, Method = "Card", Source = "Phone", PaymentMethodId = methodId, Status = ok ? "Failed" : "Pending",
        });
        db.ApiLogs.Add(new ApiLog
        {
            CustomerId = c.Id, Api = "Stripe", Action = "charges.create", Status = ok ? "not built" : "skipped",
            Request = $"{{\"amount\":{(int)Math.Round(amount * 100)},\"currency\":\"usd\",\"description\":\"Payment for {c.Account}\",\"source\":\"•••• hidden\"}}",
            Response = ok ? "The charge client hasn't been built yet: nothing was sent." : "Stripe isn't set up: nothing was sent.",
        });
        return new Result(ok
            ? "Stripe settings are there, but the charge client hasn't been built yet. Payment marked Failed."
            : $"Payment of {amount:C} recorded as Pending. The card wasn't charged: Stripe isn't set up.");
    }

    private Result AddBillCredit(decimal amount, string description)
    {
        db.LedgerEntries.Add(new LedgerEntry { CustomerId = c.Id, Kind = "credit", Amount = amount, Description = description, RequestedBy = who });
        return new Result($"Pending bill credit of {amount:C} added.");
    }

    private Result BalanceAdjustment(decimal amount, string reason)
    {
        db.Payments.Add(new Payment
        {
            CustomerId = c.Id, Reference = "ADJ-" + Code(), PaidOn = DateOnly.FromDateTime(DateTime.Today), PaidTime = TimeOnly.FromDateTime(DateTime.Now),
            Amount = amount, Method = "Adjustment", Source = "Corral", Kind = "Balance Adjustment", Status = "Success",
        });
        c.Balance -= amount;
        return new Result($"Balance adjusted by -{amount:C}. Reason: {reason}");
    }

    private async Task<Result> PaymentPlan(string product, string flag, string queue, int installments, DateOnly firstDue)
    {
        await Products.AddAsync(db, c, product, who, $"installments: {installments} · first due: {firstDue:yyyy-MM-dd}");
        Flag(flag);
        EnterQueue(queue);
        return new Result($"{product}: {installments} installments starting {firstDue:yyyy-MM-dd}.");
    }

    private async Task<Result> TransferPayment(int paymentId, string account)
    {
        var other = await OtherAccount(account);
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId && p.CustomerId == c.Id) ?? throw new ActionProblem("Pick one of this account's payments.");
        payment.CustomerId = other.Id;
        db.Notes.Add(new Note { CustomerId = other.Id, Author = who, Body = $"Payment {payment.Reference} transferred in from account {c.Account}." });
        return new Result($"Payment {payment.Reference} moved to account {other.Account}.");
    }

    private async Task<Result> TransferStars(int stars, string account)
    {
        var other = await OtherAccount(account);
        if (stars > c.Stars) throw new ActionProblem($"This account only has {c.Stars:N0} stars.");
        db.StarEntries.Add(new StarEntry { CustomerId = c.Id, Stars = -stars, Reason = $"Transferred to {other.Account}", By = who });
        db.StarEntries.Add(new StarEntry { CustomerId = other.Id, Stars = stars, Reason = $"Transferred from {c.Account}", By = who });
        c.Stars -= stars;
        other.Stars += stars;
        return new Result($"{stars:N0} stars transferred to account {other.Account}.");
    }

    private async Task<Result> BalancedBilling()
    {
        await Products.AddAsync(db, c, "Balanced Billing", who);
        EnterQueue("QueueCartBalancedBilling");
        return new Result("Balanced billing set up.");
    }

    private async Task<Result> TransferService(string street, string city, string zip, string esiid, DateOnly moveIn)
    {
        var oldEsiid = c.Esiid;
        foreach (var a in await db.ServiceAddresses.Where(a => a.CustomerId == c.Id && a.ServiceEnd == null).ToListAsync()) a.ServiceEnd = moveIn.AddDays(-1);
        db.ServiceAddresses.Add(new ServiceAddress { CustomerId = c.Id, Esiid = esiid, Street = street, City = city, Zip = zip, ServiceStart = moveIn });
        Ercot("814_24", "Move Out", moveIn.AddDays(-1), oldEsiid);
        Ercot("814_16", "MoveIn", moveIn, esiid);
        (c.Address, c.City, c.Zip, c.Esiid) = (street, city, zip, esiid);
        await MoveToQueue("QueueCartTransition");
        return new Result($"Service transferring to {street}, {city} on {moveIn:yyyy-MM-dd}.");
    }

    private Result AddAuthorizedUser(string name, string phone)
    {
        c.AuthorizedUsers = (c.AuthorizedUsers is { Length: > 0 } a ? a + "\n" : "") + $"{name.Replace("|", " ")}|{phone.Replace("|", " ")}|{DateTime.Today:yyyy-MM-dd}";
        return new Result($"{name} added as an authorized user.");
    }

    private Result MeterRequest(string label, string queue, string message, string type = "650_01")
    {
        Ercot(type, label);
        EnterQueue(queue);
        return new Result(message);
    }

    private static string Code() => Convert.ToHexString(Guid.NewGuid().ToByteArray())[..8];
}

/// <summary>Something about the request is wrong (the agent sees the message).</summary>
public class ActionProblem(string message) : Exception(message);
