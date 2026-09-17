namespace ReservationService.Models;

public enum ReservationStatus
{
    Reserved,
    CheckedOut,
    Returned,
    Cancelled
}

public enum BookCondition
{
    Good,
    Fair,
    Poor,
    Damaged
}

public enum WaitlistStatus
{
    Waiting,
    Notified,
    Claimed,
    Expired,
    Cancelled
}