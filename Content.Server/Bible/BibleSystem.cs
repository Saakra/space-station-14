using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.MobState.Components;
using Content.Shared.Damage;
using Content.Shared.Verbs;
using Content.Shared.Tag;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Server.Cooldown;
using Content.Server.Bible.Components;
using Content.Server.Popups;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Bible
{
    public sealed class BibleSystem : EntitySystem
    {
        [Dependency] private readonly InventorySystem _invSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;
        [Dependency] private readonly ActionBlockerSystem _blocker = default!;
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BibleComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<SummonableComponent, GetVerbsEvent<AlternativeVerb>>(AddSummonVerb);
            SubscribeLocalEvent<SummonableComponent, GetItemActionsEvent>(GetSummonAction);
            SubscribeLocalEvent<SummonableComponent, SummonActionEvent>(OnSummon);
        }

        private void OnAfterInteract(EntityUid uid, BibleComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach)
                return;

            var currentTime = _gameTiming.CurTime;

            if (currentTime < component.CooldownEnd)
            {
                return;
            }
            if (args.Target == null || args.Target == args.User || !HasComp<MobStateComponent>(args.Target))
            {
                return;
            }

            component.LastAttackTime = currentTime;
            component.CooldownEnd = component.LastAttackTime + TimeSpan.FromSeconds(component.CooldownTime);
            RaiseLocalEvent(uid, new RefreshItemCooldownEvent(component.LastAttackTime, component.CooldownEnd), false);

            if (!HasComp<BibleUserComponent>(args.User))
            {
                _popupSystem.PopupEntity(Loc.GetString("bible-sizzle"), args.User, Filter.Entities(args.User));

                SoundSystem.Play(Filter.Pvs(args.User), component.SizzleSoundPath.GetSound(), args.User);
                _damageableSystem.TryChangeDamage(args.User, component.DamageOnUntrainedUse, true);

                return;
            }

            if (!_invSystem.TryGetSlotEntity(args.Target.Value, "head", out var entityUid) && !_tagSystem.HasTag(args.Target.Value, "Familiar"))
            {
                if (_random.Prob(component.FailChance))
                {
                var othersFailMessage = Loc.GetString(component.LocPrefix + "-heal-fail-others", ("user", args.User),("target", args.Target),("bible", uid));
                _popupSystem.PopupEntity(othersFailMessage, args.User, Filter.Pvs(args.User).RemoveWhereAttachedEntity(puid => puid == args.User));

                var selfFailMessage = Loc.GetString(component.LocPrefix + "-heal-fail-self", ("target", args.Target),("bible", uid));
                _popupSystem.PopupEntity(selfFailMessage, args.User, Filter.Entities(args.User));

                SoundSystem.Play(Filter.Pvs(args.Target.Value), "/Audio/Effects/hit_kick.ogg", args.User);
                _damageableSystem.TryChangeDamage(args.Target.Value, component.DamageOnFail, true);
                return;
                }
            }

            var othersMessage = Loc.GetString(component.LocPrefix + "-heal-success-others", ("user", args.User),("target", args.Target),("bible", uid));
            _popupSystem.PopupEntity(othersMessage, args.User, Filter.Pvs(args.User).RemoveWhereAttachedEntity(puid => puid == args.User));

            var selfMessage = Loc.GetString(component.LocPrefix + "-heal-success-self", ("target", args.Target),("bible", uid));
            _popupSystem.PopupEntity(selfMessage, args.User, Filter.Entities(args.User));

            SoundSystem.Play(Filter.Pvs(args.Target.Value), component.HealSoundPath.GetSound(), args.User);
            _damageableSystem.TryChangeDamage(args.Target.Value, component.Damage, true);
        }

        private void AddSummonVerb(EntityUid uid, SummonableComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess || component.AlreadySummoned || component.SpecialItemPrototype == null)
                return;

            if (component.RequiresBibleUser && !HasComp<BibleUserComponent>(args.User))
                return;

            AlternativeVerb verb = new()
            {
                Act = () =>
                {
                    TransformComponent? position = Comp<TransformComponent>(args.User);
                    AttemptSummon(component, args.User, position);
                },
                Text = Loc.GetString("bible-summon-verb"),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }

        private void GetSummonAction(EntityUid uid, SummonableComponent component, GetItemActionsEvent args)
        {
            if (component.AlreadySummoned)
                return;

            args.Actions.Add(component.SummonAction);
        }
        private void OnSummon(EntityUid uid, SummonableComponent component, SummonActionEvent args)
        {
            AttemptSummon(component, args.Performer, Transform(args.Performer));
        }
        private void AttemptSummon(SummonableComponent component, EntityUid user, TransformComponent? position)
        {
            if (component.AlreadySummoned || component.SpecialItemPrototype == null)
                return;
            if (component.RequiresBibleUser && !HasComp<BibleUserComponent>(user))
                return;
            if (!Resolve(user, ref position))
                return;
            if (component.Deleted || Deleted(component.Owner))
                return;
            if (!_blocker.CanInteract(user, component.Owner))
                return;

            EntityManager.SpawnEntity(component.SpecialItemPrototype, position.Coordinates);
            component.AlreadySummoned = true;
            _actionsSystem.RemoveAction(user, component.SummonAction);
        }
    }

    public sealed class SummonActionEvent : InstantActionEvent
    {}
}
