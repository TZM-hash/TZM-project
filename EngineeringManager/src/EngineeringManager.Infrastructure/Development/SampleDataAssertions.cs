namespace EngineeringManager.Infrastructure.Development;

public static class SampleDataAssertions
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"样例数据一致性失败：{message}");
        }
    }
}
