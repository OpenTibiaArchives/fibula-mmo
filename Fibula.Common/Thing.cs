﻿// -----------------------------------------------------------------
// <copyright file="Thing.cs" company="2Dudes">
// Copyright (c) | Jose L. Nunez de Caceres et al.
// https://linkedin.com/in/nunezdecaceres
//
// All Rights Reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------

namespace Fibula.Common
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Fibula.Common.Contracts;
    using Fibula.Common.Contracts.Abstractions;
    using Fibula.Common.Contracts.Delegates;
    using Fibula.Common.Contracts.Structs;
    using Fibula.Common.Utilities;

    /// <summary>
    /// Class that represents all things in the game.
    /// </summary>
    public abstract class Thing : IThing
    {
        /// <summary>
        /// Holds this thing's parent container.
        /// </summary>
        private IThingContainer parentContainer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Thing"/> class.
        /// </summary>
        public Thing()
        {
            this.UniqueId = Guid.NewGuid();
        }

        /// <summary>
        /// Event to invoke when any of the properties of this thing have changed.
        /// </summary>
        public event OnThingStateChanged ThingChanged;

        /// <summary>
        /// Gets the unique id of this item.
        /// </summary>
        public Guid UniqueId { get; }

        /// <summary>
        /// Gets the type id of this thing.
        /// </summary>
        public abstract ushort TypeId { get; }

        /// <summary>
        /// Gets a value indicating whether this thing can be moved.
        /// </summary>
        public abstract bool CanBeMoved { get; }

        /// <summary>
        /// Gets or sets the parent container of this thing.
        /// </summary>
        public IThingContainer ParentContainer
        {
            get
            {
                return this.parentContainer;
            }

            set
            {
                var oldLocation = this.Location;

                this.parentContainer = value;

                // Note that this.Location accounts for the parent container's location
                // That's why we check if these are now considered different.
                if (oldLocation != this.Location)
                {
                    this.InvokePropertyChanged(nameof(this.Location));
                }
            }
        }

        /// <summary>
        /// Gets this thing's location.
        /// </summary>
        public virtual Location Location
        {
            get
            {
                return this.ParentContainer?.Location ?? default;
            }
        }

        /// <summary>
        /// Gets the location where this thing is being carried at, if any.
        /// </summary>
        public abstract Location? CarryLocation { get; }

        /// <summary>
        /// Invokes the <see cref="ThingChanged"/> event on this thing.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        public void InvokePropertyChanged(string propertyName)
        {
            propertyName.ThrowIfNullOrWhiteSpace(propertyName);

            this.ThingChanged?.Invoke(this, new ThingStateChangedEventArgs() { PropertyChanged = propertyName });
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.DescribeForLogger();
        }

        /// <summary>
        /// Provides a string describing the current thing for logging purposes.
        /// </summary>
        /// <returns>The string to log.</returns>
        public abstract string DescribeForLogger();

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">The other object to compare against.</param>
        /// <returns>True if the current object is equal to the other parameter, false otherwise.</returns>
        public bool Equals([AllowNull] IThing other)
        {
            return this.UniqueId == other?.UniqueId;
        }
    }
}
