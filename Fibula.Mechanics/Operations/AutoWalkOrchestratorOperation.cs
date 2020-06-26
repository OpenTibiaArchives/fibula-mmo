﻿// -----------------------------------------------------------------
// <copyright file="AutoWalkOrchestratorOperation.cs" company="2Dudes">
// Copyright (c) 2018 2Dudes. All rights reserved.
// Author: Jose L. Nunez de Caceres
// jlnunez89@gmail.com
// http://linkedin.com/in/jlnunez89
//
// Licensed under the MIT license.
// See LICENSE.txt file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------

namespace Fibula.Mechanics.Operations
{
    using System;
    using Fibula.Common.Contracts.Enumerations;
    using Fibula.Common.Utilities;
    using Fibula.Creatures.Contracts.Abstractions;
    using Fibula.Creatures.Contracts.Constants;
    using Fibula.Mechanics.Contracts.Abstractions;
    using Fibula.Mechanics.Contracts.Enumerations;
    using Fibula.Mechanics.Contracts.Extensions;
    using Fibula.Mechanics.Operations.Arguments;

    /// <summary>
    /// Class that represents an operation that orchestrates auto walk operations.
    /// </summary>
    public class AutoWalkOrchestratorOperation : Operation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutoWalkOrchestratorOperation"/> class.
        /// </summary>
        /// <param name="creature">The creature that is auto walking.</param>
        public AutoWalkOrchestratorOperation(ICreature creature)
            : base(creature?.Id ?? 0)
        {
            creature.ThrowIfNull(nameof(creature));

            this.Creature = creature;
        }

        /// <summary>
        /// Gets the type of exhaustion that this operation produces.
        /// </summary>
        public override ExhaustionType ExhaustionType => ExhaustionType.None;

        /// <summary>
        /// Gets or sets the exhaustion cost time of this operation.
        /// </summary>
        public override TimeSpan ExhaustionCost { get; protected set; }

        /// <summary>
        /// Gets the combatant that is attacking on this operation.
        /// </summary>
        public ICreature Creature { get; }

        /// <summary>
        /// Executes the operation's logic.
        /// </summary>
        /// <param name="context">A reference to the operation context.</param>
        protected override void Execute(IOperationContext context)
        {
            if (this.Creature == null ||
                this.Creature.WalkPlan == null ||
                this.Creature.WalkPlan.Value.State != WalkPlanState.InProgress ||
                !this.Creature.WalkPlan.Value.GoingAsIntended(this.Creature.Location) ||
                this.Creature.WalkPlan.Value.Waypoints.Count == 0)
            {
                return;
            }

            var nextLocation = this.Creature.WalkPlan.Value.Waypoints.First.Value;

            // Normalize delay to protect against negative time spans.
            var scheduleDelay = TimeSpan.Zero;

            var autoWalkOp = context.OperationFactory.Create(
                new MovementOperationCreationArguments(
                    this.Creature.Id,
                    CreatureConstants.CreatureThingId,
                    this.Creature.Location,
                    0xFF,
                    this.Creature.Id,
                    nextLocation,
                    this.Creature.Id));

            // Add delay from current exhaustion of the requestor, if any.
            if (this.Creature is ICreatureWithExhaustion creatureWithExhaustion)
            {
                // The scheduling delay becomes any cooldown debt for this operation.
                scheduleDelay = creatureWithExhaustion.CalculateRemainingCooldownTime(autoWalkOp.ExhaustionType, context.Scheduler.CurrentTime);
            }

            // Schedule the actual walk operation.
            context.Scheduler.ScheduleEvent(autoWalkOp, scheduleDelay);

            this.RepeatAfter = this.Creature.CalculateStepDuration(this.Creature.Location.DirectionTo(nextLocation), context.Map.GetTileAt(this.Creature.Location));
        }
    }
}