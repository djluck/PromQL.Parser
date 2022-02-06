namespace PromQL.Parser
{
    /// <summary>
    /// Taken from https://github.com/prometheus/prometheus/blob/277bf93952b56227cb750a8129197efa489eddde/promql/parser/value.go#L26-L32. 
    /// </summary>
    public enum ValueType
    {
        None,
        Scalar,
        Vector,
        Matrix,
        String,
    }
}