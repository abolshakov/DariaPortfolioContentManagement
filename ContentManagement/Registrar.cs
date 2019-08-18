using System;
using System.Collections.Generic;

namespace ContentManagement
{
    internal class Registrar
    {
        private readonly HashSet<int> _existing;

        public Registrar()
        {
            _existing = new HashSet<int>();
        }

        public void RegisterIds(IEnumerable<int> existing)
        {
	        foreach (var value in existing)
	        {
		        _existing.Add(value);
	        }
        }

        public IEnumerable<int> RegisteredIds()
        {
	        return _existing;
        }

        public int NextId()
        {
            int id;
            var random = new Random((int)DateTime.Now.Ticks);

            do
            {
                id = random.Next(1000000, 9999999);
            } while (_existing.Contains(id));

            _existing.Add(id);

            return id;
        }
    }
}
