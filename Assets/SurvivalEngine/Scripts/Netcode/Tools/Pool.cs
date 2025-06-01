using System.Collections;
using System.Collections.Generic;

namespace NetcodePlus
{

    public class Pool<T> where T : new()
    {
        private List<T> all_data = new List<T>();
        private Stack<T> stack = new Stack<T>();

        public T Create()
        {
            if (stack.Count > 0)
                return stack.Pop();
            T new_obj = new T();
            all_data.Add(new_obj);
            return new_obj;
        }

        public void Dispose(T elem)
        {
            if(elem != null)
                stack.Push(elem);
        }

        public void DisposeAll()
        {
            stack.Clear();
            foreach (T obj in all_data)
                stack.Push(obj);
        }

        public void Clear()
        {
            all_data.Clear();
            stack.Clear();
        }

        public List<T> GetAll()
        {
            return all_data;
        }

        public int Count
        {
            get { return all_data.Count; }
        }
    }
}
