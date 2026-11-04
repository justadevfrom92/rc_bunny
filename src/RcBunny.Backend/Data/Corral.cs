namespace RcBunny.Data;

/// <summary>The fixed lists Corral uses (ebunny's config/corral.php and config/admin.php).</summary>
public static class Corral
{
    /// <summary>Account statuses and the colour each shows in: ok, warn, bad or info.</summary>
    public static readonly (string Status, string Tone)[] Statuses =
    [
        ("Submitted", "info"), ("Pending - Credit", "warn"), ("Pending - Deposit Due", "warn"), ("Pending - No Deposit Due", "warn"),
        ("Pending - Utility Not Answered", "warn"), ("Good - On Flow", "ok"), ("Rejected - By Utility", "bad"), ("Dropped - Churned", "bad"),
        ("Pending - Disconnect", "warn"), ("Disconnected", "bad"), ("Moved Out", "bad"), ("Cancelled", "bad"),
    ];

    public static string StatusTone(string status) => Statuses.FirstOrDefault(s => s.Status == status).Tone ?? "";

    /// <summary>What can hold an order up.</summary>
    public static readonly string[] Exceptions =
        ["Not sent to UtiliBill", "Not sent to Utility", "No ESIID", "Non-Resi Meter", "Permit Required", "Switch Hold", "Possible Duplicate", "Other Exception"];

    public static readonly string[] Flags =
    [
        "VIP - PUC Complaint", "VIP - Critical Care", "VIP - Payment Arrangement", "VIP - Deferred Payment Plan",
        "VIP - Inadvertent Gain", "VIP - Inadvertent Loss", "VIP - Billing Error", "VIP - Friends & Family", "VIP - Other",
        "SOLAR CUSTOMER", "Returned Mail", "Fraudulent Order", "Multiple Enrollments", "Order has been cancelled manually",
    ];

    /// <summary>Flags that put a banner at the top of the account page: flag → (title, message).</summary>
    public static readonly Dictionary<string, (string Title, string Text)> FlagBanners = new()
    {
        ["SOLAR CUSTOMER"] = ("SOLAR CUSTOMER", "Handle with care!"),
        ["VIP - Friends & Family"] = ("Friends and Family", "This customer is a Friends and Family customer. Please escalate to the back office!"),
        ["VIP - PUC Complaint"] = ("PUC Complaint", "Open PUC complaint. Escalate to a supervisor before making changes."),
        ["VIP - Critical Care"] = ("Critical Care", "Critical care customer. Do not disconnect."),
        ["Fraudulent Order"] = ("Fraudulent Order", "Verify identity before discussing the account."),
    };

    /// <summary>Products that can be on an account.</summary>
    public static readonly string[] Products =
    [
        "AutoPay", "Paperless Billing", "Rangler Rewards", "Peak Perks", "Peak Perks+", "Giddyup Guarantee", "Freedom Flex", "Solar Buyback",
        "Solar Lead Referral", "Deposit Alternatives", "Payment Arrangement", "Deferred Payment Plan", "Balanced Billing", "Combined Billing",
        "AIG Home Warranty", "Electric System Protection Program", "Surge Protection Program", "Air Conditioning Protection Program",
        "Electric Vehicle Charger Protection Program", "ecobee", "Pick Your Due Date", "Refer-A-Friend", "ADT Security",
    ];

    public static readonly Dictionary<string, string[]> NoteCategories = new()
    {
        ["Agent Issue"] = ["Audio Issue", "Customer Disconnect/Hang up", "Customer Not Authorized", "Internal Transfer", "Problem with System", "Test", "Wrong Number"],
        ["Billing"] = ["Balance Inquiry", "Balanced Billing Inquiry", "Balanced Billing Set up", "Bill Not Received", "Billing Dispute", "DNP/RNP", "Fees", "PYDD/Due Date Change", "Rate", "Resend Bill", "Usage", "Waive Deposit/Fee"],
        ["Complaint"] = ["Billing", "Customer Service", "Deposit Refund", "Disconnection", "PA/DPP", "Provision of Service", "Rates", "Sales Misinformation", "Slamming", "Solar Buy Back"],
        ["Inquiry"] = ["Contract Inquiry", "Outage", "Update Contact Info"],
        ["MVO/Cancel/Rescission"] = ["Cancel - Changed Mind", "Cancel - Contract", "Cancel - Not Moving to Address", "Cancel - Price", "MVO", "Rescission"],
        ["Order"] = ["Change Plan", "Date Change", "Deposit Waiver Docs", "General Inquiry", "Link Accounts", "Meter/ESI Inquiry", "Military/First Responder", "MT Request/Status", "New Enrollment", "Re-run Credit", "Reject/Rejection", "Renewal", "Status Inquiry", "Switch Hold", "Transfer of Service", "Verify ID"],
        ["Payment"] = ["AutoPay Set Up", "Deposit Refund Request", "DPP Set Up", "Duplicate Payment/Refund", "Missing Payment", "PA/DPP/Extension Inquiry", "Pay Deposit", "Payment Arrangement Set up", "Payment Dispute", "Payment Made", "Pledge Inquiry", "Remove Autopay"],
        ["Ticket"] = ["Internal Note", "Manager Request", "Follow-up", "Call Note"],
        ["Products"] = ["Add ecobee", "Add Freedom Flex", "Add Home Warranty", "Add Peak Perks/Plus", "Add Solar Buy Back", "Ecobee Inquiry", "Refer A Friend (RAF)", "Remove ecobee", "Remove Freedom Flex", "Remove Home Warranty", "Remove Paperless Billing", "Remove Peak Perks/Plus", "Remove Solar Buy Back", "Solar BuyBack Inquiry"],
        ["Rewards"] = ["Gift Card", "Promo Code", "Redeem Stars"],
        ["Website/Mobile"] = ["Cannot Log in", "Online Issue", "Reset Username/Password"],
    };

    public static readonly string[] Priorities = ["Low", "Medium", "High"];

    /// <summary>How a call, email, text or chat ended, and its colour.</summary>
    public static readonly (string Resolution, string Tone)[] Resolutions =
        [("Resolved", "ok"), ("Incomplete", "warn"), ("Follow-up Needed", "info"), ("Escalated", "bad"), ("No Answer", "")];

    /// <summary>Kinds of contact you can log by hand (Corral → New Contact).</summary>
    public static readonly Dictionary<string, string> ContactChannels = new() { ["call"] = "Phone Call", ["email"] = "Email", ["sms"] = "Text", ["chat"] = "Chat" };

    /// <summary>Exception work queues: key → (name, what's in it).</summary>
    public static readonly Dictionary<string, (string Name, string About)> WorkQueues = new()
    {
        ["ercot-exceptions"] = ("ERCOT Exceptions", "Enrollment transactions rejected or stuck at ERCOT"),
        ["duplicate-ips"] = ("Duplicate IPs", "Orders placed from the same IP within 24 hours"),
        ["duplicated-payments"] = ("Duplicated Payments", "Same card and amount charged twice"),
        ["unapplied-deposits"] = ("Unapplied Deposits", "Deposits received but not applied to an account"),
        ["unapplied-payments"] = ("Unapplied Payments", "Payments not yet posted to the billing system"),
        ["unapplied-credits"] = ("Unapplied Credits", "Bill credits and promos waiting to post"),
        ["unapplied-debits"] = ("Unapplied Debits", "Manual debits waiting to post"),
        ["unapplied-plan-changes"] = ("Unapplied Plan Changes", "Renewals and plan switches not yet sent"),
        ["unapplied-transfers"] = ("Unapplied Transfers", "Move-in / move-out transfers waiting"),
        ["unbilled-orders"] = ("Unbilled Orders", "On-flow accounts with no first bill"),
        ["unprocessed-autopays"] = ("Unprocessed Autopays", "AutoPay drafts that did not run"),
        ["unprocessed-bills"] = ("Unprocessed Bills", "Bills received but not generated"),
        ["unprocessed-orders"] = ("Unprocessed Orders", "Orders waiting to be sent to the utility"),
    };

    /// <summary>Every order-flow queue an account can be moved into (TECH → Move Queues).</summary>
    public static readonly string[] OrderQueues =
    [
        "QueueBillComplete", "QueueBillException", "QueueBillReady", "QueueBillUnprocessed",
        "QueueBill", "QueueCallStart", "QueueCall", "QueueCartAbandon",
        "QueueCartAccepted", "QueueCartActive", "QueueCartAddOrder", "QueueCartArchive",
        "QueueCartAutopayRequired", "QueueCartBalancedBilling", "QueueCartBalancedue", "QueueCartBillReady",
        "QueueCartBlendExtend", "QueueCartCancel", "QueueCartCollection", "QueueCartDead",
        "QueueCartDeposit", "QueueCartDisconnectComplete", "QueueCartDisconnectNotice", "QueueCartDisconnectRejected",
        "QueueCartDisconnectSent", "QueueCartDisconnect", "QueueCartDropped", "QueueCartDropping",
        "QueueCartDualfuel", "QueueCartExceptionBalance", "QueueCartExceptionCancelled", "QueueCartExceptionChangeRenew",
        "QueueCartExceptionCommercialMatrix", "QueueCartExceptionCommercial", "QueueCartExceptionDuplicate", "QueueCartExceptionEmailDropped",
        "QueueCartExceptionErcotCancel", "QueueCartExceptionErcotDatechange", "QueueCartExceptionErcotDisconnect", "QueueCartExceptionErcotEnrollment",
        "QueueCartExceptionErcotMoveout", "QueueCartExceptionErcotOther", "QueueCartExceptionErcotReconnect", "QueueCartExceptionErcot",
        "QueueCartExceptionEsiid", "QueueCartExceptionFraud", "QueueCartExceptionMultiple", "QueueCartExceptionOther",
        "QueueCartExceptionPermitRequired", "QueueCartExceptionPlan", "QueueCartExceptionProvisional", "QueueCartExceptionSuspect",
        "QueueCartExceptionSwitchHold", "QueueCartExceptionTest", "QueueCartExceptionTransfer", "QueueCartExceptionUnprocessed",
        "QueueCartException", "QueueCartFulfilledAccepted", "QueueCartFulfilledCancelledDropped", "QueueCartFulfilledCancelled",
        "QueueCartFulfilledChurned", "QueueCartFulfilledDepositPaid", "QueueCartFulfilledDeposit", "QueueCartFulfilledDocusign",
        "QueueCartFulfilledFlowing", "QueueCartFulfilledHold", "QueueCartFulfilledImport", "QueueCartFulfilledOutboxHold",
        "QueueCartFulfilledOutbox", "QueueCartFulfilledPending", "QueueCartFulfilledRejectedAutopay", "QueueCartFulfilledRejectedCredit",
        "QueueCartFulfilledRejectedError", "QueueCartFulfilledRejectedExisting", "QueueCartFulfilledRejectedFrozen", "QueueCartFulfilledRejectedPermitRequired",
        "QueueCartFulfilledRejectedPlan", "QueueCartFulfilledRejectedSwitchhold", "QueueCartFulfilledRejectedTransfer", "QueueCartFulfilledRejectedUan",
        "QueueCartFulfilledRejected", "QueueCartFulfilledRequested", "QueueCartFulfilled", "QueueCartMeterMvo",
        "QueueCartMeterRead", "QueueCartMeterReconnect", "QueueCartMeterTest", "QueueCartMeter",
        "QueueCartMvo", "QueueCartPassthru", "QueueCartPaymentArrangementDefaulted", "QueueCartPromoApplied",
        "QueueCartReconnectComplete", "QueueCartReconnectRejected", "QueueCartReconnectSent", "QueueCartReconnect",
        "QueueCartReturnedmail", "QueueCartStart", "QueueCartSubmit", "QueueCartSwitchhold",
        "QueueCartSystemdown", "QueueCartTextCancelled", "QueueCartText", "QueueCartTransition",
        "QueueCartUsagerequest", "QueueCartVipBilling", "QueueCartVipCriticalcare", "QueueCartVipDeferredpayment",
        "QueueCartVipFriendsandfamily", "QueueCartVipIag", "QueueCartVipIal", "QueueCartVipOther",
        "QueueCartVipPaymentarrangement", "QueueCartVipPuc", "QueueCartVip", "QueueCartWinbackComplete",
        "QueueCartWinbackSent", "QueueCartWinback", "QueueCartWriteoff", "QueueCart",
        "QueueForecastComplete", "QueueForecastProcessing", "QueueForecastStart", "QueueForecast",
        "QueueLedgerPending", "QueueLedgerSent", "QueueLedgerSkipped", "QueueLedger",
        "QueueMyaccountHidden", "QueueMyaccountPlanChanged", "QueueMyaccountPlanRenewed", "QueueMyaccountTransferred",
        "QueueMyaccount", "QueueOfferRateAccepted", "QueueOfferRatePending", "QueueOfferRate",
        "QueueOffer", "QueuePartnerDeposit", "QueuePartnerProvisional", "QueuePartnerSherpa",
        "QueuePartnerWhitelisted", "QueuePartner", "QueueProductCombinedBilling", "QueueProductDepositAlt",
        "QueueProductFreedomFixComplete", "QueueProductFreedomFixReady", "QueueProductFreedomFix", "QueueProductGiddyupAccepted",
        "QueueProductGiddyupExpired", "QueueProductGiddyupPending", "QueueProductGiddyupReady", "QueueProductGiddyupRejected",
        "QueueProductGiddyup", "QueueProductSolar", "QueueProduct", "Queue",
    ];

    /// <summary>A field on an account action's form: name, label and type (text, textarea, money, number, date, method, payment, template).</summary>
    public record ActionField(string Name, string Label, string Type);

    /// <summary>An account action: key, label, the menu it's in (status or service), its fields, and an extra permission it needs.</summary>
    public record AccountAction(string Key, string Label, string Menu, ActionField[] Fields, string? Permission = null);

    private static ActionField F(string name, string label, string type = "text") => new(name, label, type);

    public static readonly AccountAction[] Actions =
    [
        new("credit-run", "Credit - Run Credit", "status", []),
        new("credit-max-deposit", "Credit - Give Max Deposit", "status", []),
        new("credit-waive-deposit", "Credit - Waive Deposit", "status", [F("reason", "Reason")]),
        new("send-to-market", "Status - Send to Market", "status", [F("date", "Requested start", "date")]),
        new("temp-disconnect", "Status - Temp Disconnect", "status", [F("date", "Disconnect date", "date")]),
        new("disconnect", "Status - Disconnect", "status", [F("date", "Disconnect date", "date")]),
        new("reconnect", "Status - Reconnect", "status", []),
        new("move-out", "Status - Move Out", "status", [F("date", "Move-out date", "date")]),
        new("cancel-order", "Status - Cancel Order", "status", [F("reason", "Reason")]),
        new("delete-order", "Status - Delete Order", "status", [], "delete"),
        new("winback", "Status - Winback", "status", []),
        new("clear-exceptions", "Misc - Clear Current Exceptions", "status", []),
        new("repair-plan", "Misc - Repair Plan", "status", []),
        new("undo-last-plan", "Misc - Undo Last Plan", "status", []),
        new("add-order", "Misc - Add Order", "status", []),
        new("make-payment", "Billing - Make Payment", "service", [F("amount", "Amount ($)", "money"), F("method", "Payment method", "method")]),
        new("add-bill-credit", "Billing - Add Bill Credit", "service", [F("amount", "Amount ($)", "money"), F("description", "Description")]),
        new("balance-adjustment", "Billing - Add Balance Adjustment Payment", "service", [F("amount", "Amount ($)", "money"), F("description", "Reason")]),
        new("payment-arrangement", "Billing - Payment Arrangement", "service", [F("installments", "Installments", "number"), F("date", "First due date", "date")]),
        new("deferred-payment-plan", "Billing - Deferred Payment Plan", "service", [F("installments", "Installments", "number"), F("date", "First due date", "date")]),
        new("transfer-payments", "Billing - Transfer Payments", "service", [F("payment", "Payment", "payment"), F("account", "To account #")]),
        new("transfer-stars", "Billing - Transfer Stars", "service", [F("stars", "Stars", "number"), F("account", "To account #")]),
        new("balanced-billing", "Billing - Balanced Billing", "service", []),
        new("transfer-service", "Plan - Transfer Service", "service", [F("street", "New street"), F("city", "City"), F("zip", "Zip"), F("esiid", "New ESIID"), F("date", "Move-in date", "date")]),
        new("change-plan", "Plan - Change Plan", "service", []),
        new("spend-stars", "MyAccount - Spend Stars", "service", []),
        new("reset-login", "MyAccount - Reset Login", "service", []),
        new("add-authorized-user", "MyAccount - Add Authorized User", "service", [F("name", "Name"), F("phone", "Phone")]),
        new("send-email", "Contact - Send Email", "service", [F("template", "Template", "template"), F("message", "Message", "textarea")]),
        new("send-text", "Contact - Send Text", "service", [F("message", "Message", "textarea")]),
        new("test-meter", "Meter - Test Meter", "service", []),
        new("reread", "Meter - ReRead", "service", []),
        new("request-usage", "Meter - Request Usage", "service", []),
    ];

    /// <summary>Email templates agents can send from an account (Contact - Send Email).</summary>
    public static readonly string[] EmailTemplates =
        ["Welcome Letter", "Welcome Letter - Renewed", "Winback Offer", "MyAccount - Password Reset", "Payment Reminder", "Disconnect Notice", "Renewal Offer", "Deposit Request"];
}
