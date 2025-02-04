﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osuTK;

namespace osu.Framework.Graphics.UserInterface
{
    /// <summary>
    /// A single-row control to display a list of selectable tabs along with an optional right-aligned dropdown
    /// containing overflow items (tabs which cannot be displayed in the allocated width). Includes
    /// support for pinning items, causing them to be displayed before all other items at the
    /// start of the list.
    /// </summary>
    /// <remarks>
    /// If a multi-line (or vertical) tab control is required, <see cref="TabFillFlowContainer.AllowMultiline"/> must be set to true.
    /// Without this, <see cref="TabControl{T}"/> will automatically hide extra items.
    /// </remarks>
    /// <typeparam name="T">The type of item to be represented by tabs.</typeparam>
    public abstract class TabControl<T> : CompositeDrawable, IHasCurrentValue<T>, IKeyBindingHandler<PlatformAction>
    {
        private readonly Bindable<T> current = new Bindable<T>();

        private Bindable<T> currentBound;

        public Bindable<T> Current
        {
            get => current;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (currentBound != null) current.UnbindFrom(currentBound);
                current.BindTo(currentBound = value);
            }
        }

        /// <summary>
        /// A list of items currently in the tab control in the order they are dispalyed.
        /// </summary>
        public IEnumerable<T> Items
        {
            get
            {
                var items = TabContainer.TabItems.Select(t => t.Value);

                if (Dropdown != null)
                    items = items.Concat(Dropdown.Items).Distinct();

                return items;
            }
        }

        public IEnumerable<T> VisibleItems => TabContainer.TabItems.Select(t => t.Value).Distinct();

        /// <summary>
        /// When true, tabs selected from the overflow dropdown will be moved to the front of the list (after pinned items).
        /// </summary>
        public bool AutoSort { set; get; }

        protected Dropdown<T> Dropdown;

        protected readonly TabFillFlowContainer TabContainer;

        protected IReadOnlyDictionary<T, TabItem<T>> TabMap => tabMap;

        protected TabItem<T> SelectedTab;

        /// <summary>
        /// When true, tabs can be switched back and forth using PlatformAction.DocumentPrevious and PlatformAction.DocumentNext respectively.
        /// </summary>
        public virtual bool IsSwitchable => true;

        /// <summary>
        /// Creates an optional overflow dropdown.
        /// When implementing this dropdown make sure:
        ///  - It is made to be anchored to the right-hand side of its parent.
        ///  - The dropdown's header does *not* have a relative x axis.
        /// </summary>
        protected abstract Dropdown<T> CreateDropdown();

        /// <summary>
        /// Creates a tab item.
        /// </summary>
        protected abstract TabItem<T> CreateTabItem(T value);

        /// <summary>
        /// Decremented each time a tab needs to be inserted at the start of the list.
        /// </summary>
        private int depthCounter;

        /// <summary>
        /// A mapping of tabs to their items.
        /// </summary>
        private readonly Dictionary<T, TabItem<T>> tabMap;

        private bool firstSelection = true;

        protected TabControl()
        {
            Dropdown = CreateDropdown();

            if (Dropdown != null)
            {
                Dropdown.RelativeSizeAxes = Axes.X;
                Dropdown.Anchor = Anchor.TopRight;
                Dropdown.Origin = Anchor.TopRight;
                Dropdown.Current = Current;

                AddInternal(Dropdown);

                Trace.Assert(Dropdown.Header.Anchor.HasFlag(Anchor.x2), $@"The {nameof(Dropdown)} implementation should use a right-based anchor inside a TabControl.");
                Trace.Assert(!Dropdown.Header.RelativeSizeAxes.HasFlag(Axes.X), $@"The {nameof(Dropdown)} implementation's header should have a specific size.");

                // create tab items for already existing items in dropdown (if any).
                tabMap = Dropdown.Items.ToDictionary(item => item, item => addTab(item, false));
            }
            else
                tabMap = new Dictionary<T, TabItem<T>>();

            AddInternal(TabContainer = CreateTabFlow());
            TabContainer.TabVisibilityChanged = updateDropdown;
            TabContainer.ChildrenEnumerable = tabMap.Values;

            Current.ValueChanged += _ => firstSelection = false;
        }

        protected override void Update()
        {
            base.Update();

            if (Dropdown != null)
            {
                Dropdown.Header.Height = DrawHeight;
                TabContainer.Padding = new MarginPadding { Right = Dropdown.Header.Width };
            }
        }

        // Default to first selection in list
        protected override void LoadComplete()
        {
            if (firstSelection && !Current.Disabled && Items.Any())
                Current.Value = Items.First();

            Current.BindValueChanged(v => selectTab(v.NewValue != null ? tabMap[v.NewValue] : null), true);
        }

        /// <summary>
        /// Pin an item to the start of the list.
        /// </summary>
        /// <param name="item">The item to pin.</param>
        public void PinItem(T item)
        {
            if (!tabMap.TryGetValue(item, out TabItem<T> tab))
                return;

            tab.Pinned = true;
        }

        /// <summary>
        /// Unpin an item and return it to the start of unpinned items.
        /// </summary>
        /// <param name="item">The item to unpin.</param>
        public void UnpinItem(T item)
        {
            if (!tabMap.TryGetValue(item, out TabItem<T> tab))
                return;

            tab.Pinned = false;
        }

        /// <summary>
        /// Add a new item to the control.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public void AddItem(T item) => addTab(item);

        /// <summary>
        /// Removes an item from the control.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        public void RemoveItem(T item) => removeTab(item);

        /// <summary>
        /// Removes all items from the control.
        /// </summary>
        public void Clear() => tabMap.Keys.ToArray().ForEach(item => removeTab(item));

        private TabItem<T> addTab(T value, bool addToDropdown = true)
        {
            // Do not allow duplicate adding
            if (tabMap.ContainsKey(value))
                throw new InvalidOperationException($"Item {value} has already been added to this {nameof(TabControl<T>)}");

            var tab = CreateTabItem(value);
            AddTabItem(tab, addToDropdown);

            return tab;
        }

        private void removeTab(T value, bool removeFromDropdown = true)
        {
            if (!tabMap.ContainsKey(value))
                throw new InvalidOperationException($"Item {value} doesn't exist in this {nameof(TabControl<T>)}.");

            RemoveTabItem(tabMap[value], removeFromDropdown);
        }

        /// <summary>
        /// Adds an arbitrary <see cref="TabItem{T}"/> to the control.
        /// </summary>
        /// <param name="tab">The tab to add.</param>
        /// <param name="addToDropdown">Whether the tab should be added to the Dropdown if supported by the <see cref="TabControl{T}"/> implementation.</param>
        protected virtual void AddTabItem(TabItem<T> tab, bool addToDropdown = true)
        {
            tab.PinnedChanged += performTabSort;

            tab.ActivationRequested += SelectTab;

            tabMap[tab.Value] = tab;
            if (addToDropdown)
                Dropdown?.AddDropdownItem(tab.Value);
            TabContainer.Add(tab);
        }

        /// <summary>
        /// Removes a <see cref="TabItem{T}"/> from this <see cref="TabControl{T}"/>.
        /// </summary>
        /// <param name="tab">The tab to remove.</param>
        /// <param name="removeFromDropdown">Whether the tab should be removed from the Dropdown if supported by the <see cref="TabControl{T}"/> implementation.</param>
        protected virtual void RemoveTabItem(TabItem<T> tab, bool removeFromDropdown = true)
        {
            if (!tab.IsRemovable)
                throw new InvalidOperationException($"Cannot remove non-removable tab {tab}. Ensure {nameof(TabItem.IsRemovable)} is set appropriately.");

            if (tab == SelectedTab)
                SelectedTab = null;

            tabMap.Remove(tab.Value);

            if (removeFromDropdown)
                Dropdown?.RemoveDropdownItem(tab.Value);

            TabContainer.Remove(tab);
        }

        /// <summary>
        /// Callback on the change of visibility of a tab.
        /// Used to update the item's status in the overflow dropdown if required.
        /// </summary>
        private void updateDropdown(TabItem<T> tab, bool isVisible)
        {
            if (isVisible)
                Dropdown?.HideItem(tab.Value);
            else
                Dropdown?.ShowItem(tab.Value);
        }

        protected virtual void SelectTab(TabItem<T> tab)
        {
            selectTab(tab);
            Current.Value = SelectedTab != null ? SelectedTab.Value : default;
        }

        private void selectTab(TabItem<T> tab)
        {
            // Only reorder if not pinned and not showing
            if (AutoSort && tab != null && !tab.IsPresent && !tab.Pinned)
                performTabSort(tab);

            // Deactivate previously selected tab
            if (SelectedTab != null && SelectedTab != tab) SelectedTab.Active.Value = false;

            SelectedTab = tab;

            if (SelectedTab != null)
                SelectedTab.Active.Value = true;
        }

        /// <summary>
        /// Switches the currently selected tab forward or backward one index, optionally wrapping.
        /// </summary>
        /// <param name="direction">Pass 1 to move to the next tab, or -1 to move to the previous tab.</param>
        /// <param name="wrap">If <c>true</c>, moving past the start or the end of the tab list will wrap to the opposite end.</param>
        public virtual void SwitchTab(int direction, bool wrap = true)
        {
            if (Math.Abs(direction) != 1)
                throw new ArgumentException("value must be -1 or 1", nameof(direction));

            TabItem<T>[] switchableTabs = TabContainer.TabItems.Where(tab => tab.IsSwitchable).ToArray();
            int tabCount = switchableTabs.Length;

            if (tabCount == 0)
                return;

            if (tabCount == 1 || SelectedTab == null)
            {
                SelectTab(switchableTabs[0]);
                return;
            }

            int selectedIndex = Array.IndexOf(switchableTabs, SelectedTab);
            int targetIndex = selectedIndex + direction;

            if (wrap)
            {
                targetIndex %= tabCount;
                if (targetIndex < 0)
                    targetIndex += tabCount;
            }

            targetIndex = Math.Min(tabCount - 1, Math.Max(0, targetIndex));

            SelectTab(switchableTabs[targetIndex]);
        }

        private void performTabSort(TabItem<T> tab)
        {
            TabContainer.SetLayoutPosition(tab, getTabDepth(tab));

            // IsPresent of TabItems is based on Y position.
            // We reset it here to allow tabs to get a correct initial position.
            tab.Y = 0;
        }

        private float getTabDepth(TabItem<T> tab) => tab.Pinned ? float.MinValue : --depthCounter;

        public bool OnPressed(PlatformAction action)
        {
            if (IsSwitchable)
            {
                switch (action.ActionType)
                {
                    case PlatformActionType.DocumentNext:
                        SwitchTab(1);
                        return true;

                    case PlatformActionType.DocumentPrevious:
                        SwitchTab(-1);
                        return true;
                }
            }

            return false;
        }

        public bool OnReleased(PlatformAction action) => false;

        protected virtual TabFillFlowContainer CreateTabFlow() => new TabFillFlowContainer
        {
            Direction = FillDirection.Full,
            RelativeSizeAxes = Axes.Both,
            Depth = -1,
            Masking = true
        };

        public class TabFillFlowContainer : FillFlowContainer<TabItem<T>>
        {
            private bool allowMultiline;

            /// <summary>
            /// Whether tabs should be allowed to flow beyond a single line. If set to false, overflowing tabs will be automatically hidden.
            /// </summary>
            public bool AllowMultiline
            {
                get => allowMultiline;
                set
                {
                    if (value == allowMultiline)
                        return;

                    allowMultiline = value;
                    InvalidateLayout();
                }
            }

            /// <summary>
            /// Gets called whenever the visibility of a tab in this container changes. Gets invoked with the <see cref="TabItem"/> whose visibility changed and the new visibility state (true = visible, false = hidden).
            /// </summary>
            public Action<TabItem<T>, bool> TabVisibilityChanged;

            /// <summary>
            /// The list of tabs currently displayed by this container.
            /// </summary>
            public IEnumerable<TabItem<T>> TabItems => FlowingChildren.OfType<TabItem<T>>();

            protected override IEnumerable<Vector2> ComputeLayoutPositions()
            {
                foreach (var child in Children)
                    child.Y = 0;

                var result = base.ComputeLayoutPositions().ToArray();
                int i = 0;

                foreach (var child in FlowingChildren.OfType<TabItem<T>>())
                {
                    bool isVisible = allowMultiline || result[i].Y == 0;
                    updateChildIfNeeded(child, isVisible);

                    yield return result[i];

                    i++;
                }
            }

            private readonly Dictionary<TabItem<T>, bool> tabVisibility = new Dictionary<TabItem<T>, bool>();

            private void updateChildIfNeeded(TabItem<T> child, bool isVisible)
            {
                if (!tabVisibility.ContainsKey(child) || tabVisibility[child] != isVisible)
                {
                    TabVisibilityChanged?.Invoke(child, isVisible);
                    tabVisibility[child] = isVisible;

                    if (isVisible)
                        child.Show();
                    else
                        child.Hide();
                }
            }

            public override void Clear(bool disposeChildren)
            {
                tabVisibility.Clear();
                base.Clear(disposeChildren);
            }

            public override bool Remove(TabItem<T> drawable)
            {
                tabVisibility.Remove(drawable);
                return base.Remove(drawable);
            }
        }
    }
}
