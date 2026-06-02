using Microsoft.EntityFrameworkCore;

namespace Bondstone.EntityFrameworkCore.Inbox;

public interface IInboxClaimConflictDetector
{
    bool IsClaimConflict(DbUpdateException exception);
}
