using Content.Shared.Access.Components;
using Content.Server.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Server.Popups;
using Robust.Shared.Player;

namespace Content.Server.Access.Systems
{
    public sealed class AgentIDCardSystem : SharedAgentIdCardSystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly IdCardSystem _cardSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AgentIDCardComponent, AfterInteractEvent>(OnAfterInteract);
            // BUI
            SubscribeLocalEvent<AgentIDCardComponent, AgentIDCardNameChangedMessage>(OnNameChanged);
            SubscribeLocalEvent<AgentIDCardComponent, AgentIDCardJobChangedMessage> (OnJobChanged);

        }

        private void OnAfterInteract(EntityUid uid, AgentIDCardComponent component, AfterInteractEvent args)
        {
            if (!TryComp<AccessComponent>(args.Target, out var targetAccess) || !TryComp<IdCardComponent>(uid, out var targetIDCard) || args.Target == null)
                return;

            if (!TryComp<AccessComponent>(uid, out var access) || !TryComp<IdCardComponent>(uid, out var idCard))
                return;

            var beforeLength = access.Tags.Count;
            access.Tags.UnionWith(targetAccess.Tags);
            var addedLength = access.Tags.Count - beforeLength;

            if (addedLength == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("agent-id-no-new", ("card", args.Target)), args.Target.Value, Filter.Pvs(args.User));
                return;
            }
            else if (addedLength == 1)
            {
                _popupSystem.PopupEntity(Loc.GetString("agent-id-new-1", ("card", args.Target)), args.Target.Value, Filter.Pvs(args.User));
                return;
            }
            _popupSystem.PopupEntity(Loc.GetString("agent-id-new", ("number", addedLength), ("card", args.Target)), args.Target.Value, Filter.Pvs(args.User));
        }

        private void OnJobChanged(EntityUid uid, AgentIDCardComponent comp, AgentIDCardJobChangedMessage args)
        {
            if (!TryComp<IdCardComponent>(uid, out var idCard))
                return;

            _cardSystem.TryChangeJobTitle(uid, args.Job, idCard);
        }

        private void OnNameChanged(EntityUid uid, AgentIDCardComponent comp, AgentIDCardNameChangedMessage args)
        {
            if (!TryComp<IdCardComponent>(uid, out var idCard))
                return;

            _cardSystem.TryChangeFullName(uid, args.Name, idCard);
        }
    }
}
