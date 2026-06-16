using AICourseTester.DTO;
using AICourseTester.Models;
using Microsoft.CodeAnalysis;

namespace AICourseTester.Services
{
    public class AlphaBetaService
    {
        public static readonly Random random = new Random();

        public static void PrepareTree(ProblemTree<ABNode> tree)
        {
            if (tree.Head == null)
            {
                throw new InvalidOperationException("AlphaBeta tree root is not set.");
            }

            tree.Head.depth = 0;
            _prepareNode(tree.Head, null);
        }

        private static void _prepareNode(ABNode curr, ABNode? prev)
        {
            if (curr.SubNodes == null)
            {
                return;
            }
            curr.prv = prev;
            if (prev != null)
            {
                curr.depth = prev.depth + 1;
            }
            foreach (var subNode in curr.SubNodes)
            {
                _prepareNode(subNode, curr);
            }
        }

        public static AlphaBetaSolutionDTO Search(ProblemTree<ABNode> tree)
        {
            PrepareTree(tree);
            List<ABNodeDTO> solution = new List<ABNodeDTO>();
            List<int> path = new List<int>();
            _searchSubNode(tree.Head!, solution);
            _correctSubNode(tree.Head!, path);
            return new AlphaBetaSolutionDTO() { Nodes = solution, Path = path.ToArray() };
        }

        private static (int, int) _searchSubNode(ABNode node, List<ABNodeDTO> solution)
        {
            if (node.SubNodes == null)
            {
                solution.Add(new ABNodeDTO(node));
                return (node.A, node.B);
            }
            if (node.prv != null)
            {
                node.A = node.prv.A;
                node.B = node.prv.B;
            }
            foreach (var subNode in node.SubNodes)
            {
                if (node.depth % 2 == 1)
                {
                    var (newA, newB) = _searchSubNode(subNode, solution);
                    node.B = Math.Min(Math.Min(newB, newA), node.B);
                }
                else
                {
                    var (newA, newB) = _searchSubNode(subNode, solution);
                    node.A = Math.Max(newB, Math.Max(newA, node.A));
                }
                if (node.A >= node.B)
                {
                    break;
                }
            }
            solution.Add(new ABNodeDTO(node));
            return (node.A, node.B);
        }

        private static void _correctSubNode(ABNode node, List<int> path)
        {
            if (node.SubNodes == null)
            {
                return;
            }
            var chosenNode = node.depth % 2 == 1 ? node.SubNodes.MinBy(sn => sn.A) : node.SubNodes.MaxBy(sn => sn.B);
            if (chosenNode == null)
            {
                return;
            }

            path.Add(chosenNode.Id);
            _correctSubNode(chosenNode, path);
        }

        public static ProblemTree<ABNode> GenerateTree(int height)
        {
            ProblemTree<ABNode> tree = new ProblemTree<ABNode>();
            tree.Head = new ABNode();
            tree.Head.Id = 0;
            if (height <= 1)
            {
                return tree;
            }
            int subNodesCount = random.Next(3, 6);
            tree.Head.SubNodes = new(subNodesCount);
            var id = tree.Head.Id;
            foreach (var subNode in  tree.Head.SubNodes)
            {
                id = _generateNodes(subNode, null, height - 2, id + 1);
            }
            return tree;
        }

        private static int _generateNodes(ABNode node, ABNode? prv, int height, int id)
        {
            node.Id = id;
            node.prv = prv;
            if (prv != null)
            {
                node.depth = prv.depth + 1;
            }
            if (height == 0)
            {
                int value = random.Next(0, 11);
                node.B = node.A = value;
                return id;
            }
            node.SubNodes = new List<ABNode>();
            int lim = random.Next(2, 5);
            for (int i = 0; i < lim; i++)
            {
                node.SubNodes.Add(new ABNode());
            }
            foreach (var subNode in node.SubNodes)
            {
                id = _generateNodes(subNode, node, height - 1, id + 1);
            }
            return id;
        }

        public static ProblemTree<ABNode> GenerateTree3(int maxValue, int template)
        {
            if (maxValue < 4 || template < 0 || template > 4)
            {
                throw new Exception("Invalid arguments");
            }

            ProblemTree<ABNode> tree = new ProblemTree<ABNode>();
            tree.Head = new ABNode();
            tree.Head.Id = 0;
            int subNodesCount = random.Next(3, 6);
            tree.Head.SubNodes = new();
            for (int i = 0; i < subNodesCount; i++)
            {
                tree.Head.SubNodes.Add(new ABNode());
            }
            var id = tree.Head.Id;
            int? max = int.MinValue;
            int[] mins = new int[subNodesCount];
            Array.Fill(mins, int.MaxValue);
            
            foreach (var (i, subNode) in tree.Head.SubNodes.Select((v, i) => (i, v)))
            {
                max = mins.Where(n => n != int.MaxValue).DefaultIfEmpty().Max();

                subNode.Id = ++id;
                subNode.prv = tree.Head;

                int leavesCount = random.Next(3, 5);
                subNode.SubNodes = new();
                for (int k = 0; k < leavesCount; k++)
                {
                    subNode.SubNodes.Add(new ABNode());
                }

                var values = Enumerable.Range(1, maxValue).ToList();
                foreach (var (j, leave) in subNode.SubNodes.Select((v, i) => (i, v)))    
                {
                    leave.Id = ++id;
                    leave.prv = subNode;

                    int value;
                    if (template == 4)
                    {
                        value = random.Next(1, maxValue + 1);
                    }
                    else if (i == 0)
                    {
                        var idx = random.Next(1, values.Count);
                        value = values[idx];
                        values.RemoveAt(idx);                       
                    }
                    else if (i == 1 && template == 2)
                    {
                        value = random.Next(mins[0], maxValue + 1);
                    }
                    else if (template < 3)
                    {
                        value = random.Next(1, maxValue + 1);
                        if (j == leavesCount - 2 && mins[template - 1] < mins[i])
                        {
                            value = random.Next(1, mins[template - 1] + 1);
                        }
                    } 
                    else
                    {
                        value = j == leavesCount - 1 ? random.Next(1, maxValue + 1) : random.Next((int)max + 1, maxValue + 1);
                    }
                    mins[i] = Math.Min(mins[i], value);
                    leave.A = value;
                    leave.B = value;
                }
            }
            return tree;
        }
    }
}
