using Models.MappingTasks;

namespace Infrastructure
{
    public interface IAppTaskMapperConfigurator
    {
        AppTask? MapAppTask();
    }
}
