namespace MantelProgrammingTest.Services;

public interface ILogFileService
{
    LogValidationResult Validate(string content);
    LogInterrogationResult InterrogateLogFile(string content);
}