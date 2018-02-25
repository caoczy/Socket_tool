using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WpfApplication1
{
    class IOContextPool
    {
        List<SocketAsyncEventArgs> pool;
        Int32 capacity;
        public Int32 boundary;
        public Int32 Count;

        public IOContextPool(Int32 capacity)
        {
            this.pool = new List<SocketAsyncEventArgs>(capacity);
            this.boundary = 0;
            this.capacity = capacity;
        }

        public bool Add(SocketAsyncEventArgs e)
        {
            if (e != null && pool.Count < this.capacity)
            {
                this.pool.Add(e);
                this.boundary++;
                return true;
            }
            else
            {
                return false;
            }
        }

        public SocketAsyncEventArgs Get(int index)
        {
            if (index >= 0 && index < capacity)
                return pool[index];
            else
                return null;
        }

        public SocketAsyncEventArgs Pop()
        {
            lock (this.pool)
            {
                if (this.boundary > 0)
                {
                    --this.boundary;
                    return this.pool[this.boundary];
                }
                else
                {
                    return null;
                }
            }
        }

        public bool Push(SocketAsyncEventArgs e)
        {
            if (e != null)
            {
                lock (this.pool)
                {
                    int index = this.pool.IndexOf(e, boundary);
                    if (index == boundary)
                    {
                        boundary++;
                    }
                    else
                    {
                        this.pool[index] = this.pool[boundary];
                        this.pool[boundary++] = e;
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
