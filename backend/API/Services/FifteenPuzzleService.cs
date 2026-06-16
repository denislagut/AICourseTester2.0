using AICourseTester.Models;
using Microsoft.IdentityModel.Tokens;

namespace AICourseTester.Services
{
    public class FifteenPuzzleService
    {
        public static readonly Func<ANode, int>[] Heuristics = [Heuristic1, Heuristic2];
        public static readonly Random random = new Random();

        public static ANode GenerateState(int iters, int heuristic, int dimensions = 3)
        {
            if (heuristic == 1)
            {
                return GenerateStateH1(dimensions, iters);
            }
            return GenerateStateH2(Math.Min(iters, 3));
        }
        public static ANode GenerateStateH1(int dimensions, int iters)
        {
            ANode aNode = new ANode() { State = new int[dimensions][] };
            for (int i = 0; i < dimensions; i++)
            {
                aNode.State[i] = new int[dimensions];
                Array.Fill(aNode.State[i], -1);
            }
            var range = Enumerable.Range(1, dimensions * dimensions - 1).ToList();
            int P0 = random.Next(1, dimensions * dimensions);
            aNode.State[P0 / dimensions][P0 % dimensions] = -2;
            for (int i = 0; i < iters; i++)
            {
                List<(int x, int y)> directions = new List<(int, int)>();
                if (P0 / dimensions > 0 && aNode.State[(P0 / dimensions) - 1][P0 % dimensions] == -1)
                {
                    directions.Add((P0 % dimensions, (P0 / dimensions) - 1));
                }
                if (P0 / dimensions < dimensions - 1 && aNode.State[(P0 / dimensions) + 1][P0 % dimensions] == -1)
                {
                    directions.Add((P0 % dimensions, (P0 / dimensions) + 1));
                }
                if (P0 % dimensions > 0 && aNode.State[P0 / dimensions][(P0 - 1) % dimensions] == -1)
                {
                    directions.Add(((P0 - 1) % dimensions, P0 / dimensions));
                }
                if (P0 % dimensions < dimensions - 1 && aNode.State[P0 / dimensions][(P0 + 1) % dimensions] == -1)
                {
                    directions.Add(((P0 + 1) % dimensions, P0 / dimensions));
                }
                if (i != iters - 1)
                {
                    directions.Remove((0, 0));
                }
                if (directions.IsNullOrEmpty())
                {
                    break;
                }
                var tile = directions[random.Next(0, directions.Count)];
                aNode.State[tile.y][tile.x] = P0;
                range.Remove(P0);
                P0 = tile.y * dimensions + tile.x;
            }
            for (int i = 0; i < dimensions; i++)
            {
                for (int j = 0; j < dimensions; j++)
                {
                    if (aNode.State[i][j] == -1)
                    {
                        var idx = random.Next(0, range.Count);
                        aNode.State[i][j] = range[idx];
                        range.RemoveAt(idx);
                    }
                    else if (aNode.State[i][j] == -2)
                    {
                        aNode.State[i][j] = 0;
                    }
                }
            }
            return aNode;
        }

        private static List<(int x, int y)> CollectRows(bool[] rows, int[][]state, bool onFalse=false)
        {
            List<(int x, int y)> result = new();
            for (int i = 0; i < 3; i++)
            {
                var flag = rows[i];
                if (onFalse)
                {
                    if (flag)
                    {
                        continue;
                    }
                    flag = true;
                }
                if (!flag)
                {
                    continue;
                }
                for (int j = 0; j < 3; j++)
                {
                    if (state[i][j] > 0)
                    {
                        result.Add((j, i));
                    }
                }
            }
            return result;
        }

        private static List<(int x, int y)> CollectCollumns(bool[] columns, int[][] state, bool onFalse = false)
        {
            List<(int x, int y)> result = new();
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var flag = columns[j];
                    if (onFalse)
                    {
                        if (flag)
                        {
                            continue;
                        }
                        flag = true;
                    }
                    if (flag && state[i][j] > 0)
                    {
                        result.Add((j, i));
                    }
                }
            }
            return result;
        }

        public static ANode GenerateStateH2(int iters)
        {
            int dimensions = 3;
            ANode aNode = new ANode() { State = new int[3][] };
            for (int i = 0; i < dimensions; i++)
            {
                aNode.State[i] = new int[dimensions];
                Array.Fill(aNode.State[i], -1);
            }
            var range = new ANode(3).State;
            int A0 = random.Next(0, 4);
            A0 = A0 / 2 * dimensions * (dimensions - 1) + A0 % 2 * (dimensions - 1);
            aNode.State[A0 / dimensions][A0 % dimensions] = 0;
            
            bool[] AColumns = [false, false, false];
            AColumns[A0 % dimensions] = true;
            bool[] ARows = [false, false, false];
            ARows[A0 / dimensions] = true;

            for (int i = 0; i < iters; i++)
            {
                List<((int x, int y), bool isSide)> directions = new ();
                if (A0 / dimensions > 0 && aNode.State[(A0 / dimensions) - 1][A0 % dimensions] == -1)
                {
                    directions.Add(((A0 % dimensions, (A0 / dimensions) - 1), false));
                }
                if (A0 / dimensions < dimensions - 1 && aNode.State[(A0 / dimensions) + 1][A0 % dimensions] == -1)
                {
                    directions.Add(((A0 % dimensions, (A0 / dimensions) + 1), false));
                }
                if (A0 % dimensions > 0 && aNode.State[A0 / dimensions][(A0 - 1) % dimensions] == -1)
                {
                    directions.Add((((A0 - 1) % dimensions, A0 / dimensions), true));
                }
                if (A0 % dimensions < dimensions - 1 && aNode.State[A0 / dimensions][(A0 + 1) % dimensions] == -1)
                {
                    directions.Add((((A0 + 1) % dimensions, A0 / dimensions), true));
                }
                
                if (directions.IsNullOrEmpty())
                {
                    System.Console.WriteLine($"Iteration: {i}");
                    break;
                }
                var Ai = directions[random.Next(0, directions.Count)];
                directions.Remove(Ai);
                ((int x, int y), bool isSide) Di = new();
                (int x, int y) pos;
                List<(int x, int y)> vals;
                int value;
                if (directions.Count > 0)
                {
                    Di = directions[random.Next(0, directions.Count)];
                    vals = Di.isSide ? vals = CollectCollumns(AColumns, range, true) : CollectRows(ARows, range, true);
                    if (i == 0)
                    {
                        if (Di.isSide && A0 % dimensions == dimensions - 1)
                        {
                            vals.Remove((0, 1));
                        }
                        else if (!Di.isSide && A0 == dimensions * dimensions - 1)
                        {
                            vals.Remove((1, 0));
                        }
                    } 
                    if (vals.IsNullOrEmpty())
                    {
                        (Di, Ai) = (Ai, Di);
                    }
                    vals = Di.isSide ? vals = CollectCollumns(AColumns, range, true) : CollectRows(ARows, range, true);
                    if (vals.IsNullOrEmpty())
                    {
                        vals = CollectRows([true, true, true], range);
                    }
                    pos = vals[random.Next(0, vals.Count)];
                    value = range[pos.y][pos.x];
                    range[pos.y][pos.x] = 0;
                    aNode.State[Di.Item1.y][Di.Item1.x] = value;
                }
                vals = Ai.isSide ? CollectCollumns(AColumns, range) : CollectRows(ARows, range);
                if (vals.IsNullOrEmpty())
                {
                    vals = CollectRows([true, true, true], range);
                }
                pos = vals[random.Next(0, vals.Count)];
                value = range[pos.y][pos.x];
                range[pos.y][pos.x] = 0;
                aNode.State[Ai.Item1.y][Ai.Item1.x] = value;
                
                AColumns[Ai.Item1.x] = true;
                ARows[Ai.Item1.y] = true;
                A0 = Ai.Item1.y * dimensions + Ai.Item1.x;
            }
            var tilesLeft = CollectCollumns([true, true, true], range).Select((p) => range[p.y][p.x]).ToList();
            for (int i = 0; i < dimensions; i++)
            {
                for (int j = 0; j < dimensions; j++)
                {
                    if (aNode.State[i][j] == -1)
                    {
                        var idx = random.Next(0, tilesLeft.Count);
                        aNode.State[i][j] = tilesLeft[idx];
                        tilesLeft.RemoveAt(idx);
                    }
                }
            }
            return aNode;
        }

        public static void ShuffleState(ANode node, int moves = 30)
        {
            if (node.State == null) return;
            var (x, y) = (-1, -1);
            for (int i = 0; i < node.State.Length; i++)
            {
                for (int j = 0; j < node.State[0].Length; j++)
                {
                    if (node.State[i][j] == 0)
                    {
                        (x, y) = (j, i);
                        break;
                    }
                }
                if (x != -1)
                {
                    break;
                }
            }

            (int, int) lastMove = (1, 0);
            for (int i = 0; i < moves; i++)
            {
                (int, int)[] directions = [(-1, 0), (0, -1), (1, 0), (0, 1)];
                int lastMoveIdx = Array.IndexOf(directions, lastMove);
                directions = directions.Where(val => val != directions[(lastMoveIdx + 2) % 4]).ToArray();
                int moveIdx;
                (int x, int y) move;
                int nX, nY;
                while (true)
                {
                    moveIdx = random.Next(0, directions.Length);
                    move = directions[moveIdx];
                    (nX, nY) = (x + move.x, y + move.y);
                    if (nX < 0 || nY < 0 || nX >= node.State.Length || nY >= node.State[0].Length)
                    {
                        directions = directions.Where(val => val != move).ToArray();
                        continue;
                    }
                    lastMove = move;
                    break;
                }
                (node.State[y][x], node.State[nY][nX]) = (node.State[nY][nX], node.State[y][x]);
                (x, y) = (nX, nY);
            }
        }
        public static void PrepareTree(ProblemTree<ANode> tree)
        {
            if (tree.Head == null)
            {
                throw new InvalidOperationException("FifteenPuzzle tree root is not set.");
            }

            tree.Head.depth = 0;
            _prepareNode(tree.Head, null);
        }

        private static void _prepareNode(ANode curr, ANode? prev)
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

        public static ProblemTree<ANode> ListToTree(List<ANode> nodes)
        {
            ProblemTree<ANode> tree = new();
            foreach (var node in nodes)
            {
                if (node.Parents.Count == 0)
                {
                    tree.Head = node;
                }
                foreach (var subNode in nodes)
                {
                    if (node.SubNodesIds.Count == 0 || node.Id == subNode.Id)
                    {
                        continue;
                    }
                    if (node.SubNodes == null)
                    {
                        node.SubNodes = new();
                    }
                    if (node.SubNodesIds.Contains(subNode.Id))
                    {
                        node.SubNodes.Add(subNode);
                    }
                }
            }
            PrepareTree(tree);
            return tree;
        }

        public static void GenerateNextStates(ANode node, ICollection<ANode>? ignore)
        {
            var state = node.State;
            var (ox, oy) = (-1, -1);
            bool flag = false;
            for (int i = 0; i < state.Length; i++)
            {
                if (flag)
                {
                    break;
                }
                for (int j = 0; j < state[0].Length; j++)
                {
                    if (state[i][j] == 0)
                    {
                        (ox, oy) = (j, i);
                        flag = true;
                        break;
                    }

                }
            }
            if (ox == -1)
            {
                throw new Exception("Incorrect state");
            }

            if (node.SubNodes == null)
            {
                node.SubNodes = new();
            }

            foreach (var (x, y) in ((int, int)[])[(-1, 0), (1, 0), (0, -1), (0, 1)])
            {
                int Nox = ox + x, Noy = oy + y;
                if (Nox < 0 || Noy < 0 || Nox >= state[0].Length || Noy >= state.Length)
                {
                    continue;
                }
                int[][] newState = new int[node.State.Length][];
                for (int i = 0; i < node.State.Length; i++)
                {
                    newState[i] = (int[])node.State[i].Clone();
                }
                (newState[oy][ox], newState[Noy][Nox]) = (newState[Noy][Nox], newState[oy][ox]);

                if (ignore != null)
                {
                    var n = _containsState(newState, ignore);
                    if (n != null)
                    {
                        if (n.depth == node.depth + 1)
                        {
                            n.Parents.Add(node.Id);
                            node.SubNodes.Add(n);
                        }
                        continue;
                    }
                }
                ANode newNode = new ANode();
                newNode.depth = node.depth + 1;
                newNode.Parents.Add(node.Id);
                newNode.State = newState;
                node.SubNodes.Add(newNode);
                if (ignore != null)
                {
                    ignore.Add(newNode);
                }
            }
        }

        private static ANode? _containsState(int[][] state, ICollection<ANode> nodes)
        {
            foreach (var item in nodes)
            {
                var flag = true;
                for (int i = 0; i < item.State.Length; i++)
                {
                    if (!item.State[i].SequenceEqual(state[i]))
                    {
                        flag = false;
                        break;
                    }
                }
                if (flag)
                {
                    return item;
                }
            }
            return null;
        }

        private static int GetTreeIters(ProblemTree<ANode> tree)
        {
            int iters = 0;
            ANode curr = tree.Head ?? throw new InvalidOperationException("FifteenPuzzle tree root is not set.");
            while (curr.SubNodes != null)
            {
                iters++;
                curr = curr.SubNodes[0];
            }
            return iters;
        }

        public static List<ANodeDTO> Search(ProblemTree<ANode> tree, Func<ANode, int> h)
        {
            OrderedSet<ANode> openNodes = new(state => state.F)
            {
                tree.Head ?? throw new InvalidOperationException("FifteenPuzzle tree root is not set.")
            };
            tree.Head.G = 0;
            tree.Head.H = h(tree.Head);
            tree.Head.F = tree.Head.G + tree.Head.H;
            OrderedSet<ANode> closedNodes = new();

            int iters = GetTreeIters(tree);
            while (iters > 0 && openNodes.Count > 0)
            {
                iters--;
                var curr = openNodes.Pop();
                if (curr.F - curr.G == 0)
                {
                    closedNodes.Add(curr);
                    return closedNodes.Select((n, i) => new ANodeDTO(n, i)).Concat(openNodes.Select(n => new ANodeDTO(n))).ToList();
                }

                closedNodes.Add(curr);
                if (curr.SubNodes == null)
                {
                    return closedNodes.Select((n, i) => new ANodeDTO(n, i)).Concat(openNodes.Select(n => new ANodeDTO(n))).ToList();
                }
                foreach (var state in curr.SubNodes)
                {
                    var item = openNodes.GetItem(state);
                    if (item != null)
                    {
                        var H = h(state);
                        var score = H + curr.G + 1;
                        if (score < item.F)
                        {
                            item.H = H;
                            item.F = score;
                            item.G = curr.G + 1;
                            item.prv = curr;
                        }
                        continue;
                    }
                    item = closedNodes.GetItem(state);
                    if (item != null)
                    {
                        var H = h(state);
                        var score = H + curr.G + 1;
                        if (score < item.F)
                        {
                            closedNodes.Remove(item);
                            item.F = score;
                            item.G = curr.G + 1;
                            item.H = H;
                            item.prv = curr;
                            openNodes.Add(item);
                        }
                        continue;
                    }
                    state.H = h(state);
                    state.G = curr.G + 1;
                    state.F = state.H + state.G;
                    state.prv = curr;
                    openNodes.Add(state);
                }
            }
            return closedNodes.Select((n, i) => new ANodeDTO(n, i)).Concat(openNodes.Select(n => new ANodeDTO(n))).ToList();
        }

        public static (ProblemTree<ANode>, List<ANode>) GenerateTree(ANode startState, int iters)
        {
            ProblemTree<ANode> tree = new ProblemTree<ANode>();
            tree.Head = startState;
            tree.Head.Id = 0;
            List<ANode> ignore = new() { tree.Head };
            if (iters < 1)
            {
                return (tree, ignore);
            }
            _generateNodes(tree.Head, iters, ignore, tree.Head.Id);
            return (tree, ignore);
        }

        private static int _generateNodes(ANode node, int height, ICollection<ANode>? ignore, int id)
        {
            node.Id = id;
            if (height == 0)
            {
                return id;
            }
            GenerateNextStates(node, ignore);
            if (node.SubNodesIds == null)
            {
                node.SubNodesIds = new();
            }
            foreach (var subNode in node.SubNodes ?? Enumerable.Empty<ANode>())
            {
                node.SubNodesIds.Add(id + 1);
                id = _generateNodes(subNode, height - 1, ignore, id + 1);
            }
            return id;
        }

        public static int Heuristic1(ANode node)
        {
            var score = 0;
            var len = node.State.Length * node.State[0].Length;
            for (int i = 1; i < len; i++)
            {
                if (node.State[i / node.State.Length][i % node.State[0].Length] != i)
                {
                    score++;
                }
            }
            return score;
        }

        public static int Heuristic2(ANode node)
        {
            var score = 0;
            for (int i = 0; i < node.State.Length; i++)
            {
                for (int j = 0; j < node.State[0].Length; j++)
                {
                    var tile = node.State[i][j];
                    if (tile == 0)
                    {
                        continue;
                    }
                    var (nx, ny) = (tile % node.State[0].Length, tile / node.State[0].Length);
                    score += Math.Abs(nx - j) + Math.Abs(ny - i);

                }
            }
            return score;
        }
    }
}
