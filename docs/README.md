# Documentation

These documents describe `jaytwo.Pdf` and `jaytwo.Ergonomics.Pdf`. Each answers a different question, so a statement belongs in exactly one of them and the others link to it.

| Document | Question it answers |
| --- | --- |
| [Charter](charter.md) | Why does this product exist, and what principles govern it? |
| [Supported capabilities](supported.md) | Which user needs and capability boundaries does the product commit to? |
| [Spec](spec/README.md) | Exactly what must each operation accept, preserve, produce, or refuse? |
| [Behavior reference](behavior.md) | What does the implementation support today? |

## Where to start

- To understand the product's purpose and what it will not become, read the charter, then supported capabilities.
- To learn what a shipped operation does today, read the behavior reference.
- To learn what an operation is meant to do, read the spec. Its index currently holds drafting notes while the operation contracts are rewritten.

## How the documents relate

Scope flows downward: the charter sets principles, supported capabilities sets scope, the spec makes each operation precise, and the behavior reference records what the code does. A capability stays in scope whether or not it is implemented. The behavior reference alone says what is shipped.

[`acceptance.md`](acceptance.md) records the outcomes the tests enforce, and [`qualification.md`](qualification.md) holds release evidence.

The previous specifications are kept in [`_retired_/`](_retired_/) as historical input for drafting. They are not the current contract.

Changes to the charter, supported capabilities, and spec need the owner's approval; see [`AGENTS.md`](../AGENTS.md).
