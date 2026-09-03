namespace PMP.Modules.Payment.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3,
}

public enum PaymentMethod
{
    Card = 0,
    BankTransfer = 1,
    Cash = 2,
    Manual = 3,
}
