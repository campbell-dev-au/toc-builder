# TOC Builder

A web tool for building Theory of Constraints Thinking Process diagrams. It enforces TOC logic rules rather than acting as a free-form drawing tool. The first diagram type is the Current Reality Tree.

## Language

**Theory of Constraints (TOC)**:
Goldratt's management methodology for finding and addressing the constraint that limits a system's performance. Here it always means this, never "table of contents".
_Avoid_: Table of contents

**Thinking Process**:
One of the TOC logic-tree diagram types used to analyse a problem and plan a solution.
_Avoid_: Diagram type, chart

**Current Reality Tree (CRT)**:
A Thinking Process that traces observed undesirable effects down to their root causes. The only diagram type in the first version.
_Avoid_: Problem tree

### Tree structure

**Entity**:
A single statement in a tree: a cause, an effect, or both.
_Avoid_: Node, box, item

**Causal link**:
A claimed sufficiency relationship from one or more causes to an effect.
_Avoid_: Edge, arrow, connection

**AND-junction**:
A grouping of causes that are only jointly sufficient for an effect.
_Avoid_: Combined arrow, AND gate

**UDE (undesirable effect)**:
An Entity the user tags as an unwanted observed condition. Position in the tree does not decide it.
_Avoid_: Symptom, problem

**Core problem**:
The Entity the user tags as the cause to act on, typically one driving many UDEs. It may have deeper causes beneath it. Position in the tree does not decide it.
_Avoid_: Key cause, main cause

**Root cause**:
An Entity with no cause beneath it but at least one effect above it (an Entity with no Causal links at all is reported as unconnected instead). Derived from structure, never tagged.
_Avoid_: Core problem (a different concept), origin

**Scope**:
A user-written statement of the problem domain for a tree, sent to the AI with each request for Suggestions.
_Avoid_: Description, prompt, context

### AI assistance

**Suggestion**:
A candidate Entity proposed by the AI only when the user explicitly requests it (a cause, an effect, or a missing co-cause for an AND-junction) and attached to the selected Entity. It is part of the tree only once the user accepts it.
_Avoid_: Recommendation, draft entity, AI node

### Logic checking

**Tree summary**:
A derived view of a tree's state: UDEs with no path to a Core problem, Core problems that explain no UDE, unconnected Entities, Root causes, and open Reservations. It only reports and never blocks.
_Avoid_: Validation panel, errors

**Categories of Legitimate Reservation (CLR)**:
The standard set of challenges (clarity, entity existence, causality existence, cause insufficiency and so on) used to test whether a Causal link holds.
_Avoid_: Validation rules, lint

**Reservation**:
A recorded challenge against a specific Causal link or Entity under one CLR category.
_Avoid_: Comment, warning
