﻿// -----------------------------------------------------------------
// <copyright file="BaseEvent.cs" company="2Dudes">
// Copyright (c) 2018 2Dudes. All rights reserved.
// Author: Jose L. Nunez de Caceres
// jlnunez89@gmail.com
// http://linkedin.com/in/jlnunez89
//
// Licensed under the MIT license.
// See LICENSE.txt file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------

namespace Fibula.Scheduling
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Fibula.Scheduling.Contracts.Abstractions;
    using Fibula.Scheduling.Contracts.Delegates;
    using Priority_Queue;

    /// <summary>
    /// Abstract class that represents the base event for scheduling.
    /// </summary>
    public abstract class BaseEvent : StablePriorityQueueNode, IEvent, IEquatable<BaseEvent>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseEvent"/> class.
        /// </summary>
        /// <param name="requestorId">Optional. The id of the creature or entity requesting the event. Default is 0.</param>
        public BaseEvent(uint requestorId = 0)
        {
            this.EventId = Guid.NewGuid().ToString("N");
            this.RequestorId = 0;

            this.RequestorId = requestorId;
        }

        /// <summary>
        /// Fired when this event is expedited.
        /// </summary>
        public event EventExpeditedDelegate Expedited;

        /// <summary>
        /// Gets a unique identifier for this event.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Gets the id of the requestor of this event, if available.
        /// </summary>
        public uint RequestorId { get; }

        /// <summary>
        /// Gets or sets the time after which this event should be repeated.
        /// The event is not repeated if the value is not positive.
        /// </summary>
        public TimeSpan RepeatAfter { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether this event has a handler hooked up for it's <see cref="Expedited"/> event.
        /// </summary>
        public bool HasExpeditionHandler => this.Expedited != null;

        /// <summary>
        /// Gets or sets a value indicating whether the event can be cancelled.
        /// </summary>
        public abstract bool CanBeCancelled { get; protected set; }

        /// <summary>
        /// Executes the event logic.
        /// </summary>
        /// <param name="context">The execution context.</param>
        public abstract void Execute(IEventContext context);

        /// <summary>
        /// Attempts to expedite this event, in other words, requesting it to be fired immediately.
        /// </summary>
        /// <returns>True if the event is successfully expedited, false otherwise.</returns>
        public bool Expedite()
        {
            if (this.Expedited == null)
            {
                return false;
            }

            return this.Expedited.Invoke(this);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">The other object to compare against.</param>
        /// <returns>True if the current object is equal to the other parameter, false otherwise.</returns>
        public bool Equals([AllowNull] BaseEvent other)
        {
            return this.EventId == other?.EventId;
        }
    }
}