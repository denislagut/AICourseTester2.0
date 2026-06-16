using Microsoft.CodeAnalysis;
using System.Text.Json.Serialization;

namespace AICourseTester.Models
{
    public class ANode : Node<ANode>
    {
        [Newtonsoft.Json.JsonIgnore]
        public int depth { get; set; } = 0;
        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public ANode? prv { get; set; } = null;
        public List<int> Parents { get; set; } = new();
        public int Id { get; set; }
        public int[][] State { get; set; } = null!;
        public int G { get; set; } = -1;
        public int H { get; set; } = -1;
        public int F { get; set; } = -1;
        [JsonIgnore]
        public List<ANode>? SubNodes { get; set; } = null;
        public List<int> SubNodesIds { get; set; } = new();

        public ANode() { }

        public ANode(int dimensions)
        {
            State = new int[dimensions][];
            int k = 0;
            for (int i = 0; i < dimensions; i++)
            {
                State[i] = new int[dimensions];
                for (int j = 0; j < dimensions; j++)
                {
                    State[i][j] = k++;
                }
            }
        }

        public object Clone()
        {
            ANode newNode = new();
            newNode.depth = depth;
            newNode.Parents = new(Parents);
            newNode.Id = Id;
            if (State != null)
            {
                newNode.State = new int[State.Length][];
                for (int i = 0; i < State[0].Length; i++)
                {
                    newNode.State[i] = (int[])State[i].Clone();
                }
            }
            newNode.SubNodesIds = new(SubNodesIds);
            return newNode;
        }

        public bool Equals(ANode? other)
        {
            if (other == null) return false;
            if (Id != other.Id || depth != other.depth) return false;
            if (!Parents.All(other.Parents.Contains)) return false;
            if (State == null && other.State == null) return true;
            if (State == null || other.State == null) return false;
            if (State.Length != other.State.Length || State[0].Length != other.State[0].Length) return false;
            for (int i = 0; i < State.Length; i++)
            {
                if (!State[i].SequenceEqual(other.State[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public void Reset()
        {
            G = F = H = -1;
        }
    }

    public class ANodeDTO
    {
        public int Id { get; set; }
        public int G { get; set; } = -1;
        public int H { get; set; } = -1;
        public int F { get; set; } = -1;
        public int OpenOrder { get; set; } = -1;

        public ANodeDTO() { }
        public ANodeDTO(ANode node, int order = -1)
        {
            Id = node.Id;
            G = node.G;
            H = node.H;
            F = node.F;
            OpenOrder = order;
        }
    }
}
