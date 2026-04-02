import {
  DebugSession,
  InitializedEvent,
  TerminatedEvent,
  ExitedEvent,
  OutputEvent,
  Thread,
  Breakpoint
} from "@vscode/debugadapter";
import { DebugProtocol } from "@vscode/debugprotocol";
import { ChildProcess, spawn } from "child_process";

class BabaShellDebugSession extends DebugSession {
  private process: ChildProcess | null = null;
  private static readonly THREAD_ID = 1;

  public constructor() {
    super();
    this.setDebuggerLinesStartAt1(true);
    this.setDebuggerColumnsStartAt1(true);
  }

  protected initializeRequest(
    response: DebugProtocol.InitializeResponse
  ): void {
    response.body = response.body || {};
    response.body.supportsConfigurationDoneRequest = true;
    response.body.supportsTerminateRequest = true;
    response.body.supportsRestartRequest = false;
    response.body.supportsFunctionBreakpoints = false;
    response.body.supportsConditionalBreakpoints = false;
    response.body.supportsHitConditionalBreakpoints = false;
    this.sendResponse(response);
    this.sendEvent(new InitializedEvent());
  }

  protected launchRequest(
    response: DebugProtocol.LaunchResponse,
    args: any
  ): void {
    const program = args.program as string | undefined;
    if (!program) {
      response.success = false;
      response.message = "Missing 'program' in launch configuration.";
      this.sendResponse(response);
      return;
    }

    const runtime = (args.runtime as string | undefined) || "babashell";
    const cwd = (args.cwd as string | undefined) || process.cwd();
    const cliArgs = Array.isArray(args.args) ? args.args.map(String) : [];

    this.process = spawn(runtime, [program, ...cliArgs], {
      cwd,
      stdio: "pipe"
    });

    this.process.stdout?.on("data", (data) => {
      this.sendEvent(new OutputEvent(data.toString(), "stdout"));
    });
    this.process.stderr?.on("data", (data) => {
      this.sendEvent(new OutputEvent(data.toString(), "stderr"));
    });
    this.process.on("exit", (code) => {
      this.sendEvent(new ExitedEvent(code ?? 0));
      this.sendEvent(new TerminatedEvent());
    });

    this.sendResponse(response);
  }

  protected setBreakPointsRequest(
    response: DebugProtocol.SetBreakpointsResponse,
    args: DebugProtocol.SetBreakpointsArguments
  ): void {
    const breakpoints = (args.breakpoints || []).map(
      (bp) => new Breakpoint(false, bp.line)
    );
    response.body = { breakpoints };
    this.sendResponse(response);
  }

  protected threadsRequest(response: DebugProtocol.ThreadsResponse): void {
    response.body = {
      threads: [new Thread(BabaShellDebugSession.THREAD_ID, "BabaShell")]
    };
    this.sendResponse(response);
  }

  protected disconnectRequest(
    response: DebugProtocol.DisconnectResponse
  ): void {
    if (this.process) {
      this.process.kill();
      this.process = null;
    }
    this.sendResponse(response);
  }

  protected terminateRequest(
    response: DebugProtocol.TerminateResponse
  ): void {
    if (this.process) {
      this.process.kill();
      this.process = null;
    }
    this.sendResponse(response);
  }
}

DebugSession.run(BabaShellDebugSession);
