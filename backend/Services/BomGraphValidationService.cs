using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

public sealed record BomGraphValidationResult(bool HasCycle, IReadOnlyList<long> CyclePath);

/// <summary>
/// Validates the effective BOM graph that would result from publishing a candidate version.
/// </summary>
public sealed class BomGraphValidationService
{
    public BomGraphValidationResult ValidateActivation(
        OracleConnection connection,
        OracleTransaction? transaction,
        long materialId,
        long versionId)
    {
        var graph = LoadActivationGraph(connection, transaction, materialId, versionId);
        return FindCycle(graph);
    }

    private static Dictionary<long, List<long>> LoadActivationGraph(
        OracleConnection connection,
        OracleTransaction? transaction,
        long materialId,
        long versionId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.BindByName = true;
        cmd.CommandText = @"SELECT b.PARENT_MATERIAL_ID, b.CHILD_MATERIAL_ID
                            FROM MATERIAL m
                            JOIN BOM_VERSION bv ON bv.MATERIAL_ID = m.MATERIAL_ID
                            JOIN BOM b ON b.VERSION_ID = bv.VERSION_ID
                            WHERE (m.MATERIAL_ID = :materialId AND bv.VERSION_ID = :versionId)
                               OR (m.MATERIAL_ID <> :materialId
                                   AND bv.VERSION_ID = m.CURRENT_VERSION_ID
                                   AND bv.EFFECTIVE_DATE <= TRUNC(SYSDATE)
                                   AND (bv.EXPIRE_DATE IS NULL OR bv.EXPIRE_DATE >= TRUNC(SYSDATE)))";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.Parameters.Add(new OracleParameter("versionId", versionId));

        var graph = new Dictionary<long, List<long>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var parent = Convert.ToInt64(reader.GetValue(0));
            var child = Convert.ToInt64(reader.GetValue(1));
            if (!graph.TryGetValue(parent, out var children))
            {
                children = [];
                graph[parent] = children;
            }

            children.Add(child);
        }

        return graph;
    }

    private static BomGraphValidationResult FindCycle(Dictionary<long, List<long>> graph)
    {
        var state = new Dictionary<long, VisitState>();
        var stack = new List<long>();
        var nodes = graph.Keys
            .Concat(graph.Values.SelectMany(children => children))
            .Distinct()
            .OrderBy(id => id);

        foreach (var node in nodes)
        {
            if (state.GetValueOrDefault(node) != VisitState.Unvisited)
            {
                continue;
            }

            if (Visit(node, graph, state, stack, out var cyclePath))
            {
                return new BomGraphValidationResult(true, cyclePath);
            }
        }

        return new BomGraphValidationResult(false, []);
    }

    private static bool Visit(
        long node,
        IReadOnlyDictionary<long, List<long>> graph,
        Dictionary<long, VisitState> state,
        List<long> stack,
        out IReadOnlyList<long> cyclePath)
    {
        state[node] = VisitState.Visiting;
        stack.Add(node);

        if (graph.TryGetValue(node, out var children))
        {
            foreach (var child in children)
            {
                var childState = state.GetValueOrDefault(child);
                if (childState == VisitState.Unvisited
                    && Visit(child, graph, state, stack, out cyclePath))
                {
                    return true;
                }

                if (childState == VisitState.Visiting)
                {
                    var cycleStart = stack.IndexOf(child);
                    cyclePath = stack.Skip(cycleStart).Append(child).ToList();
                    return true;
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        state[node] = VisitState.Visited;
        cyclePath = [];
        return false;
    }

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}
