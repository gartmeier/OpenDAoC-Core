import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";

/**
 * Accepts the open Dialog, or declines it (`accept` false), as a click on its button does, and succeeds once the answer is sent. An OK-only Dialog is answered the same way.
 * It doesn't wait for what the server does next: the chat that follows says, such as a Healer restoring Constitution, and `agent.self` shows it.
 * It fails with `noDialog` when no Dialog is open, such as when a Healer asked nothing because no Constitution was lost.
 */
export async function answer(agent: Agent, accept: boolean): Promise<Outcome> {
  const dialog = agent.self.dialog;

  if (!dialog)
    return failed("noDialog");

  const { code, data1, data2, data3 } = dialog;
  await agent.send({ name: "answerDialog", code, data1, data2, data3, accept });
  return succeeded;
}
