using System.Collections.Generic;
using System.Net.Sockets;

namespace Socket_tool
{
    internal class IoContextPool
    {
        private readonly int _capacity;
        private readonly List<SocketAsyncEventArgs> _pool;
        public int Boundary;

        public IoContextPool(int capacity)
        {
            this._pool = new List<SocketAsyncEventArgs>(capacity);
            this.Boundary = 0;
            this._capacity = capacity;
        }

        public bool Add(SocketAsyncEventArgs e)
        {
            lock (this._pool)
            {
                if (e == null && this._pool.Count == this._capacity)
                {
                    return false;
                }

                this._pool.Add(e);
                this.Boundary++;
                return true;
            }
        }

        public SocketAsyncEventArgs Get(int index)
        {
            if (index >= 0 && index < this._capacity)
            {
                return this._pool[index];
            }

            return null;
        }

        public SocketAsyncEventArgs Pop()
        {
            lock (this._pool)
            {
                if (this.Boundary == 0 || this.Boundary < 0)
                {
                    return null;
                }

                --this.Boundary;
                return this._pool[this.Boundary];
            }
        }

        public bool Push(SocketAsyncEventArgs e)
        {
            if (e == null)
            {
                return false;
            }

            lock (this._pool)
            {
                var index = this._pool.IndexOf(e, this.Boundary);
                if (index == this.Boundary)
                {
                    this.Boundary++;
                }
                else
                {
                    this._pool[index] = this._pool[this.Boundary];
                    this._pool[this.Boundary++] = e;
                }
            }

            return true;
        }
    }
}