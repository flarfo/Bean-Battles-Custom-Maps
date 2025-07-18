﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BeanBattlesMapMaker
{
    public class Tuple<T, U>
    {
        public T Item1 { get; private set; }
        public U Item2 { get; private set; }

        public Tuple(T item1, U item2)
        {
            Item1 = item1;
            Item2 = item2;
        }
    }
}
