using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface ISnInitializable
    {
        Task InitializeAsync();
    }
}
