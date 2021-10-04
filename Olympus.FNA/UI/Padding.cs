﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OlympUI {
    public unsafe struct Padding {

        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int this[int side] {
            get => side switch {
                0 => Left,
                1 => Top,
                2 => Right,
                3 => Bottom,
                _ => throw new ArgumentOutOfRangeException(nameof(side))
            };
            set {
                fixed (Padding* self = &this) {
                    *(side switch {
                        0 => &self->Left,
                        1 => &self->Top,
                        2 => &self->Right,
                        3 => &self->Bottom,
                        _ => throw new ArgumentOutOfRangeException(nameof(side))
                    }) = value;
                }
            }
        }

    }
}
