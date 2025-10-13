using System.Threading;
using System.Threading.Tasks;

namespace VladiCore.Data.Provisioning;

public interface ISchemaBootstrapper
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
