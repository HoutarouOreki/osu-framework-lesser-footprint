﻿// Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-framework/master/LICENCE

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Transformations;
using osu.Framework.Input;
using OpenTK;
using OpenTK.Graphics;
using osu.Framework.GameModes.Testing;

namespace osu.Framework.VisualTests.Tests
{
    class TestCaseSmoothenedEdges : TestCase
    {
        public override string Name => @"Smoothened Edges";
        public override string Description => @"Boxes with automatically smoothened edges (no anti-aliasing).";

        private Box[] boxes = new Box[4];
        private Container testContainer;

        public override void Reset()
        {
            base.Reset();

            Add(testContainer = new Container()
            {
                RelativeSizeAxes = Axes.Both,
                Children = new[]
                {
                    new FlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(0.5f),
                                Children = new []
                                {
                                    boxes[0] = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.White,
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(0.5f),
                                        EdgeSmoothness = Vector2.Zero,
                                    }
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(0.5f),
                                Children = new []
                                {
                                    boxes[1] = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.White,
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(0.5f),
                                        EdgeSmoothness = new Vector2(0, 2),
                                    }
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(0.5f),
                                Children = new []
                                {
                                    boxes[2] = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.White,
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(0.5f),
                                        EdgeSmoothness = Vector2.One,
                                    }
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(0.5f),
                                Children = new []
                                {
                                    boxes[3] = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.White,
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(0.5f),
                                        EdgeSmoothness = Vector2.One * 2,
                                    }
                                }
                            },
                        }
                    }
                }
            });
        }

        protected override void Update()
        {
            base.Update();

            foreach (Drawable box in boxes)
                box.Rotation += 0.01f;
        }
    }
}
