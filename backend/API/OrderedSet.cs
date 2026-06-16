using System.Collections;

// https://stackoverflow.com/questions/1552225/hashset-that-preserves-ordering
public class OrderedSet<T> : ICollection<T> where T : notnull
{
    private readonly IDictionary<T, LinkedListNode<T>> m_Dictionary;
    private readonly LinkedList<T> m_LinkedList;
    private readonly Func<T, IComparable>? sortKey;

    public OrderedSet() : this(EqualityComparer<T>.Default) { }

    public OrderedSet(Func<T, IComparable> sortKey)
        : this(EqualityComparer<T>.Default)
    {
        this.sortKey = sortKey;
    }

    public OrderedSet(IEqualityComparer<T> comparer)
    {
        m_Dictionary = new Dictionary<T, LinkedListNode<T>>(comparer);
        m_LinkedList = new LinkedList<T>();
    }

    public int Count => m_Dictionary.Count;

    public virtual bool IsReadOnly => m_Dictionary.IsReadOnly;

    void ICollection<T>.Add(T item)
    {
        Add(item);
    }

    public bool Add(T item)
    {
        if (m_Dictionary.ContainsKey(item)) 
            return false;
        if (sortKey == null)
        {
            m_Dictionary.Add(item, m_LinkedList.AddLast(item));
            return true;
        }
        var skItem = sortKey(item);
        for (LinkedListNode<T>? i = m_LinkedList.First; i != null; i = i.Next)
        {
            if (skItem.CompareTo(sortKey(i.Value)) < 0)
            {
                m_Dictionary.Add(item, m_LinkedList.AddBefore(i, item));
                return true;
            }
        }
        m_Dictionary.Add(item, m_LinkedList.AddLast(item));
        return true;
    }

    public void Clear()
    {
        m_LinkedList.Clear();
        m_Dictionary.Clear();
    }

    public bool Remove(T item)
    {
        if (!m_Dictionary.TryGetValue(item, out var node)) return false;
        m_Dictionary.Remove(item);
        m_LinkedList.Remove(node);
        return true;
    }

    public T? GetItem(T item)
    {
        m_Dictionary.TryGetValue(item, out var node);
        if (node == null)
        {
            return default(T);
        }
        return node.Value;
    }

    public T Pop()
    {
        var first = m_LinkedList.First ?? throw new InvalidOperationException("The set is empty.");
        var item = first.Value;
        m_LinkedList.RemoveFirst();
        m_Dictionary.Remove(item);
        return item;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return m_LinkedList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Contains(T item)
    {
        return m_Dictionary.ContainsKey(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        m_LinkedList.CopyTo(array, arrayIndex);
    }
}
