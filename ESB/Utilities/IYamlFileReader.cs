namespace ESB.Common
{
    public interface IYamlFileReader
    {
        T ReadYamlFile<T>(string filename) where T : class;
    }
}