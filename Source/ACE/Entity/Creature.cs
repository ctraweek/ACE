﻿using ACE.Entity.Enum;
using ACE.Factories;
using ACE.Managers;
using ACE.Network;
using ACE.Network.Enum;
using ACE.Network.GameAction;
using ACE.Network.GameEvent.Events;
using ACE.Network.GameMessages.Messages;
using ACE.Network.Motion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACE.Entity
{
    public class Creature : Container
    {
        protected Dictionary<Enum.Ability, CreatureAbility> abilities = new Dictionary<Enum.Ability, CreatureAbility>();

        public ReadOnlyDictionary<Enum.Ability, CreatureAbility> Abilities;

        public CreatureAbility Strength { get; set; }

        public CreatureAbility Endurance { get; set; }

        public CreatureAbility Coordination { get; set; }

        public CreatureAbility Quickness { get; set; }

        public CreatureAbility Focus { get; set; }

        public CreatureAbility Self { get; set; }

        public CreatureAbility Health { get; set; }

        public CreatureAbility Stamina { get; set; }

        public CreatureAbility Mana { get; set; }

        /// <summary>
        /// This will be false when creature is dead and waits for respawn
        /// </summary>
        public bool IsAlive { get; set; }

        public Creature(ObjectType type, ObjectGuid guid, string name, ushort weenieClassId, ObjectDescriptionFlag descriptionFlag, WeenieHeaderFlag weenieFlag, Position position)
            : base(type, guid, name, weenieClassId, descriptionFlag, weenieFlag, position)
        {
        }

        public Creature(AceCreatureStaticLocation aceC)
            : base((ObjectType)aceC.CreatureData.TypeId, 
                  new ObjectGuid(CommonObjectFactory.DynamicObjectId, GuidType.Creature), 
                  aceC.CreatureData.Name,
                  aceC.WeenieClassId,
                  (ObjectDescriptionFlag)aceC.CreatureData.WdescBitField,
                  (WeenieHeaderFlag)aceC.CreatureData.WeenieFlags,
                  aceC.Position)
        {
            if (aceC.WeenieClassId < 0x8000u)
                this.WeenieClassid = aceC.WeenieClassId;
            else
                this.WeenieClassid = (ushort)(aceC.WeenieClassId - 0x8000);

            SetObjectData(aceC.CreatureData);
            SetAbilities(aceC.CreatureData);
        }

        private void SetObjectData(AceCreatureObject aco)
        {
            PhysicsData.CurrentMotionState = new GeneralMotion(MotionStance.Standing);
            PhysicsData.MTableResourceId = aco.MotionTableId;
            PhysicsData.Stable = aco.SoundTableId;
            PhysicsData.CSetup = aco.ModelTableId;
            PhysicsData.Petable = aco.PhysicsTableId;
            PhysicsData.ObjScale = aco.ObjectScale;
            
            // this should probably be determined based on the presence of data.
            PhysicsData.PhysicsDescriptionFlag = (PhysicsDescriptionFlag)aco.PhysicsBitField;
            PhysicsData.PhysicsState = (PhysicsState)aco.PhysicsState;

            // game data min required flags;
            Icon = (ushort)aco.IconId;

            GameData.Usable = (Usable)aco.Usability;
            // intersting finding: the radar color is influenced over the weenieClassId and NOT the blipcolor
            // the blipcolor in DB is 0 whereas the enum suggests it should be 2
            GameData.RadarColour = (RadarColor)aco.BlipColor;
            GameData.RadarBehavior = (RadarBehavior)aco.Radar;
            GameData.UseRadius = aco.UseRadius;

            aco.WeenieAnimationOverrides.ForEach(ao => this.ModelData.AddModel(ao.Index, (ushort)(ao.AnimationId - 0x01000000)));
            aco.WeenieTextureMapOverrides.ForEach(to => this.ModelData.AddTexture(to.Index, (ushort)(to.OldId - 0x05000000), (ushort)(to.NewId - 0x05000000)));
            aco.WeeniePaletteOverrides.ForEach(po => this.ModelData.AddPalette((ushort)(po.SubPaletteId - 0x04000000), (byte)po.Offset, (byte)(po.Length / 8)));
            ModelData.PaletteGuid = aco.PaletteId - 0x04000000;
        }

        private void SetAbilities(AceCreatureObject aco)
        {
            Strength = new CreatureAbility(aco, Enum.Ability.Strength);
            Endurance = new CreatureAbility(aco, Enum.Ability.Endurance);
            Coordination = new CreatureAbility(aco, Enum.Ability.Coordination);
            Quickness = new CreatureAbility(aco, Enum.Ability.Quickness);
            Focus = new CreatureAbility(aco, Enum.Ability.Focus);
            Self = new CreatureAbility(aco, Enum.Ability.Self);

            Health = new CreatureAbility(aco, Enum.Ability.Health);
            Stamina = new CreatureAbility(aco, Enum.Ability.Stamina);
            Mana = new CreatureAbility(aco, Enum.Ability.Mana);

            Strength.Base = aco.Strength;
            Endurance.Base = aco.Endurance;
            Coordination.Base = aco.Coordination;
            Quickness.Base = aco.Quickness;
            Focus.Base = aco.Focus;
            Self.Base = aco.Self;

            // recalculate the base value as the abilities end/will have an influence on those
            Health.Base = aco.Health - Health.UnbuffedValue;
            Stamina.Base = aco.Stamina - Stamina.UnbuffedValue;
            Mana.Base = aco.Mana - Mana.UnbuffedValue;

            Health.Current = Health.MaxValue;
            Stamina.Current = Stamina.MaxValue;
            Mana.Current = Mana.MaxValue;

            abilities.Add(Enum.Ability.Strength, Strength);
            abilities.Add(Enum.Ability.Endurance, Endurance);
            abilities.Add(Enum.Ability.Coordination, Coordination);
            abilities.Add(Enum.Ability.Quickness, Quickness);
            abilities.Add(Enum.Ability.Focus, Focus);
            abilities.Add(Enum.Ability.Self, Self);

            abilities.Add(Enum.Ability.Health, Health);
            abilities.Add(Enum.Ability.Stamina, Stamina);
            abilities.Add(Enum.Ability.Mana, Mana);

            Abilities = new ReadOnlyDictionary<Enum.Ability, CreatureAbility>(abilities);

            IsAlive = true;
        }

        public void Kill(Session session)
        {
            IsAlive = false;

            // Create and send the death notice
            string killMessage = $"{session.Player.Name} has killed {Name}.";
            var creatureDeathEvent = new GameEventDeathNotice(session, killMessage);
            session.Network.EnqueueSend(creatureDeathEvent);

            // MovementEvent: (Hand-)Combat or in the case of smite: from Standing to Death
            // TODO: Check if the duration of the motion can somehow be computed
            GeneralMotion motionDeath = new GeneralMotion(MotionStance.Standing, new MotionItem(MotionCommand.Dead));
            QueuedGameAction actionDeath = new QueuedGameAction(this.Guid.Full, motionDeath, 2.0f, true, GameActionType.MovementEvent);
            session.Player.AddToActionQueue(actionDeath);
            
            // Create Corspe and set a location on the ground
            // TODO: set text of killer in description and find a better computation for the location, some corpse could end up in the ground
            var corpse = CorpseObjectFactory.CreateCorpse(this, this.Location);
            corpse.Location.PositionY -= corpse.PhysicsData.ObjScale;
            corpse.Location.PositionZ -= corpse.PhysicsData.ObjScale / 2;

            // Remove Creature from Landblock and add Corpse in that location via the ActionQueue to honor the motion delays
            QueuedGameAction removeCreature = new QueuedGameAction(this.Guid.Full, this, true, true, GameActionType.ObjectDelete);
            QueuedGameAction addCorpse = new QueuedGameAction(this.Guid.Full, corpse, true, GameActionType.ObjectCreate);
            session.Player.AddToActionQueue(removeCreature);
            session.Player.AddToActionQueue(addCorpse);
        }
    }
}
