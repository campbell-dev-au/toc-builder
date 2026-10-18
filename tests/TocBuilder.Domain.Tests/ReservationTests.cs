namespace TocBuilder.Domain.Tests;

public class ReservationTests
{
    private static (Tree Tree, Entity Effect, Entity A, Entity B) TreeWithTwoCauses()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Orders ship late");
        var a = tree.AddCause(effect.Id, "Picking is slow");
        var b = tree.AddCause(effect.Id, "Carrier is delayed");
        return (tree, effect, a, b);
    }

    [Fact]
    public void A_reservation_can_be_raised_against_a_causal_link_and_starts_open_with_no_note()
    {
        var (tree, effect, a, _) = TreeWithTwoCauses();
        var link = new CausalLink(a.Id, effect.Id);

        var result = tree.RaiseReservation(new OnLink(link), ClrCategory.CausalityExistence);

        Assert.Equal(ReservationOutcome.Raised, result.Outcome);
        var reservation = Assert.Single(tree.Reservations);
        Assert.Equal(result.Reservation, reservation);
        Assert.Equal(new OnLink(link), reservation.Target);
        Assert.Equal(ClrCategory.CausalityExistence, reservation.Category);
        Assert.Equal(ReservationState.Open, reservation.State);
        Assert.Equal("", reservation.Note);
    }

    [Fact]
    public void Each_category_can_be_raised_once_per_target_but_other_categories_and_targets_are_independent()
    {
        var (tree, effect, a, b) = TreeWithTwoCauses();
        var linkA = new OnLink(new CausalLink(a.Id, effect.Id));
        var linkB = new OnLink(new CausalLink(b.Id, effect.Id));
        var first = tree.RaiseReservation(linkA, ClrCategory.Clarity).Reservation;

        var again = tree.RaiseReservation(linkA, ClrCategory.Clarity);

        Assert.Equal(ReservationOutcome.AlreadyRaised, again.Outcome);
        Assert.Equal(first, again.Reservation);   // hands back the existing one
        Assert.Single(tree.Reservations);
        Assert.Equal(ReservationOutcome.Raised, tree.RaiseReservation(linkA, ClrCategory.Tautology).Outcome);
        Assert.Equal(ReservationOutcome.Raised, tree.RaiseReservation(linkB, ClrCategory.Clarity).Outcome);
        Assert.Equal(3, tree.Reservations.Count);
    }

    [Fact]
    public void A_reservation_can_be_raised_against_an_entity_or_an_AND_junction()
    {
        var (tree, effect, a, b) = TreeWithTwoCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;

        var onEntity = tree.RaiseReservation(new OnEntity(effect.Id), ClrCategory.EntityExistence);
        var onJunction = tree.RaiseReservation(new OnJunction(junction.Id), ClrCategory.CauseInsufficiency);

        Assert.Equal(ReservationOutcome.Raised, onEntity.Outcome);
        Assert.Equal(ReservationOutcome.Raised, onJunction.Outcome);
        Assert.Equal([new OnEntity(effect.Id), new OnJunction(junction.Id)], tree.Reservations.Select(r => r.Target));
    }

    [Fact]
    public void A_reservation_moves_between_open_resolved_and_dismissed_and_carries_one_note()
    {
        var (tree, effect, a, _) = TreeWithTwoCauses();
        var id = tree.RaiseReservation(new OnLink(new CausalLink(a.Id, effect.Id)), ClrCategory.CauseInsufficiency).Reservation!.Id;

        tree.SetReservationState(id, ReservationState.Resolved);
        Assert.Equal(ReservationState.Resolved, tree.Reservations.Single().State);

        tree.SetReservationState(id, ReservationState.Dismissed);
        tree.SetReservationNote(id, "Not relevant here");
        var reservation = tree.Reservations.Single();
        Assert.Equal(ReservationState.Dismissed, reservation.State);
        Assert.Equal("Not relevant here", reservation.Note);

        tree.SetReservationState(id, ReservationState.Open);
        Assert.Equal(ReservationState.Open, tree.Reservations.Single().State);
        Assert.Equal(ClrCategory.CauseInsufficiency, tree.Reservations.Single().Category);   // only state and note change
    }

    [Fact]
    public void Changing_an_unknown_reservation_throws()
    {
        var tree = new Tree();

        Assert.Throws<ArgumentException>(() => tree.SetReservationState(new ReservationId(1), ReservationState.Resolved));
        Assert.Throws<ArgumentException>(() => tree.SetReservationNote(new ReservationId(1), "note"));
    }

    [Fact]
    public void Raising_and_changing_a_reservation_are_each_undoable_and_redoable()
    {
        var (tree, effect, a, _) = TreeWithTwoCauses();
        var target = new OnLink(new CausalLink(a.Id, effect.Id));
        var id = tree.RaiseReservation(target, ClrCategory.Clarity).Reservation!.Id;
        tree.SetReservationState(id, ReservationState.Resolved);
        tree.SetReservationNote(id, "Reworded");

        tree.Undo();
        Assert.Equal("", tree.Reservations.Single().Note);
        tree.Undo();
        Assert.Equal(ReservationState.Open, tree.Reservations.Single().State);
        tree.Undo();
        Assert.Empty(tree.Reservations);

        tree.Redo();
        tree.Redo();
        tree.Redo();
        var reservation = Assert.Single(tree.Reservations);
        Assert.Equal(ReservationState.Resolved, reservation.State);
        Assert.Equal("Reworded", reservation.Note);
    }

    [Fact]
    public void Undo_restores_the_reservation_id_counter_so_a_re_raised_reservation_gets_the_same_id()
    {
        var (tree, effect, a, _) = TreeWithTwoCauses();
        var target = new OnLink(new CausalLink(a.Id, effect.Id));
        var first = tree.RaiseReservation(target, ClrCategory.Clarity).Reservation!;
        tree.Undo();

        var second = tree.RaiseReservation(target, ClrCategory.Clarity).Reservation!;

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void Refused_or_unchanged_reservation_commands_leave_no_undo_step_and_keep_the_redo_history()
    {
        var (tree, effect, a, _) = TreeWithTwoCauses();
        var target = new OnLink(new CausalLink(a.Id, effect.Id));
        var id = tree.RaiseReservation(target, ClrCategory.Clarity).Reservation!.Id;
        tree.SetReservationState(id, ReservationState.Resolved);
        tree.Undo();   // leaves a redo step we must not lose

        tree.RaiseReservation(target, ClrCategory.Clarity);       // already raised
        tree.SetReservationState(id, ReservationState.Open);      // already open
        tree.SetReservationNote(id, "");                          // already empty

        Assert.True(tree.CanRedo);
        tree.Undo();
        Assert.Empty(tree.Reservations);   // the next undo removes the raise itself, so the no-ops added no steps
    }

    [Fact]
    public void A_reservation_on_an_AND_junction_stays_with_the_junction_as_causes_are_added()
    {
        var (tree, effect, a, b) = TreeWithTwoCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        var id = tree.RaiseReservation(new OnJunction(junction.Id), ClrCategory.CauseInsufficiency).Reservation!.Id;
        tree.SetReservationNote(id, "Something else is needed too");
        var c = tree.AddEntity("Stock is short");

        tree.AddToJunction(junction.Id, c.Id);
        tree.SetReservationState(id, ReservationState.Resolved);

        var reservation = Assert.Single(tree.Reservations);
        Assert.Equal(new OnJunction(junction.Id), reservation.Target);
        Assert.Equal(ReservationState.Resolved, reservation.State);
    }

    [Fact]
    public void Deleting_an_entity_removes_reservations_on_it_and_on_its_links()
    {
        var (tree, effect, a, b) = TreeWithTwoCauses();
        tree.RaiseReservation(new OnEntity(a.Id), ClrCategory.Clarity);
        tree.RaiseReservation(new OnLink(new CausalLink(a.Id, effect.Id)), ClrCategory.CausalityExistence);
        tree.RaiseReservation(new OnLink(new CausalLink(b.Id, effect.Id)), ClrCategory.CausalityExistence);

        tree.Delete(a.Id);

        Assert.Equal(new OnLink(new CausalLink(b.Id, effect.Id)), Assert.Single(tree.Reservations).Target);
    }

    [Fact]
    public void A_junction_reservation_goes_when_the_junction_is_ungrouped_dissolved_or_its_effect_deleted()
    {
        var (tree, effect, a, b) = TreeWithTwoCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        tree.RaiseReservation(new OnJunction(junction.Id), ClrCategory.CauseInsufficiency);
        tree.Ungroup(junction.Id);
        Assert.Empty(tree.Reservations);

        junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        tree.RaiseReservation(new OnJunction(junction.Id), ClrCategory.CauseInsufficiency);
        tree.RemoveFromJunction(junction.Id, b.Id);   // left with one cause, so the junction dissolves
        Assert.Empty(tree.Reservations);

        junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        tree.RaiseReservation(new OnJunction(junction.Id), ClrCategory.CauseInsufficiency);
        tree.Delete(effect.Id);
        Assert.Empty(tree.Reservations);
    }

    [Fact]
    public void Undoing_a_delete_or_ungroup_brings_back_the_reservations_that_went_with_it()
    {
        var (tree, effect, a, b) = TreeWithTwoCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        var kept = tree.RaiseReservation(new OnJunction(junction.Id), ClrCategory.CauseInsufficiency).Reservation!;
        tree.SetReservationNote(kept.Id, "keep me");
        tree.RaiseReservation(new OnEntity(a.Id), ClrCategory.Clarity);

        tree.Ungroup(junction.Id);
        tree.Undo();
        Assert.Equal(2, tree.Reservations.Count);

        tree.Delete(a.Id);
        Assert.Empty(tree.Reservations);
        tree.Undo();
        Assert.Equal(2, tree.Reservations.Count);
        Assert.Equal("keep me", tree.Reservations[0].Note);
    }

    [Fact]
    public void Only_open_reservations_appear_in_the_tree_summary()
    {
        var (tree, effect, a, b) = TreeWithTwoCauses();
        Assert.Empty(tree.Summarise().OpenReservations);
        var open = tree.RaiseReservation(new OnLink(new CausalLink(a.Id, effect.Id)), ClrCategory.CausalityExistence).Reservation!;
        var resolved = tree.RaiseReservation(new OnEntity(b.Id), ClrCategory.Clarity).Reservation!;
        var dismissed = tree.RaiseReservation(new OnEntity(effect.Id), ClrCategory.Tautology).Reservation!;
        tree.SetReservationState(resolved.Id, ReservationState.Resolved);
        tree.SetReservationState(dismissed.Id, ReservationState.Dismissed);

        Assert.Equal([open], tree.Summarise().OpenReservations);

        tree.SetReservationState(resolved.Id, ReservationState.Open);
        Assert.Equal([open.Id, resolved.Id], tree.Summarise().OpenReservations.Select(r => r.Id));
    }

    [Fact]
    public void Open_reservations_never_block_an_edit()
    {
        var (tree, effect, a, b) = TreeWithTwoCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        tree.RaiseReservation(new OnEntity(a.Id), ClrCategory.Clarity);
        tree.RaiseReservation(new OnLink(new CausalLink(a.Id, effect.Id)), ClrCategory.CausalityExistence);
        tree.RaiseReservation(new OnJunction(junction.Id), ClrCategory.CauseInsufficiency);

        tree.EditStatement(a.Id, "Picking takes too long");
        tree.SetTag(effect.Id, EntityTag.Ude);
        var extra = tree.AddCause(effect.Id, "Pallets are missing");
        Assert.Equal(JunctionOutcome.Done, tree.AddToJunction(junction.Id, extra.Id));
        Assert.Equal(LinkOutcome.Linked, tree.Link(extra.Id, b.Id));
        tree.RemoveFromJunction(junction.Id, extra.Id);
        tree.Ungroup(junction.Id);
        Assert.Equal(JunctionOutcome.Done, tree.Group(effect.Id, [a.Id, b.Id]).Outcome);
        tree.Delete(extra.Id);

        Assert.Equal("Picking takes too long", tree.Entities.Single(e => e.Id == a.Id).Statement);
        Assert.Equal(2, tree.Reservations.Count(r => r.State == ReservationState.Open));   // the junction's went with the junction when it was ungrouped
    }

    [Fact]
    public void A_reservation_against_something_not_in_the_tree_throws()
    {
        var (tree, effect, a, _) = TreeWithTwoCauses();

        Assert.Throws<ArgumentException>(() => tree.RaiseReservation(new OnEntity(new EntityId(99)), ClrCategory.Clarity));
        Assert.Throws<ArgumentException>(() => tree.RaiseReservation(new OnLink(new CausalLink(effect.Id, a.Id)), ClrCategory.Clarity));   // the link runs the other way
        Assert.Throws<ArgumentException>(() => tree.RaiseReservation(new OnJunction(new JunctionId(1)), ClrCategory.Clarity));
        Assert.Empty(tree.Reservations);
    }
}
