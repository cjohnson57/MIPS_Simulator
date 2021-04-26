using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;

namespace MIPSSimulator
{
    partial class Program
    {

        const int StartAddr = 496;
        const bool debug = false;
        static ExecutionContext ctx = new ExecutionContext(StartAddr);

        static void Main(string[] args)
        {
            string inputpath = "input.txt";
            string outputpath = "";
            int startcyle = -1;
            int endcycle = -1;
            bool disassemble = false;
            if (!debug)
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Must have at least three arguments: Inputfilename, Outputfilename, and operation. Operation can be 'dis' for disassembly, or 'sim' for simulation.");
                    Console.WriteLine("Ex.: input.txt, output.txt, dis.");
                    Environment.Exit(0);
                }
                inputpath = args[0];
                if (!File.Exists(inputpath)) //Make sure input file exists
                {
                    Console.WriteLine("First argument must specify the path to an input text file.");
                    Environment.Exit(0);
                }
                outputpath = args[1];
                if (outputpath.Length < 5 || outputpath.Substring(outputpath.Length - 4) != ".txt") //They did not append .txt, add it for them
                {
                    outputpath += ".txt";
                }
                if(args[2] != "dis" && args[2] != "sim")
                {
                    Console.WriteLine("Third argument must be 'dis' for disassembly or 'sim' for simulation.");
                    Environment.Exit(0);
                }
                disassemble = args[2] == "dis";
                if(args.Length > 3) //The last argument has also been specified
                {
                    try
                    {
                        string cycles = args[3];
                        if(cycles.Substring(0, 2) != "-T")
                        {
                            throw new Exception();
                        }
                        string[] nums = cycles.Replace("-T", "").Split(":");
                        startcyle = int.Parse(nums[0]);
                        endcycle = int.Parse(nums[1]);
                    }
                    catch
                    {
                        Console.WriteLine("Fourth argument, if specified, must be in the form -Tm:n, where m and n are non-negative integers.");
                        Console.WriteLine("-Tm:n will only display information starting at cycle m and ending at cycle n, inclusive.");
                        Console.WriteLine("-T0:0 will only display the execution summary.");
                        Console.WriteLine("If not specified, every cycle's info will be printed.");
                        Environment.Exit(0);
                    }
                }
            }

            //Read in input file and convert to list of bits
            string input = File.ReadAllText(inputpath);
            List<bool> programbits = new List<bool>();
            foreach (char c in input)
            {
                if (c == '0')
                {
                    programbits.Add(false);
                }    
                else if (c == '1')
                {
                    programbits.Add(true);
                }
            }
            string output = ""; //This will be appended to as the program runs and eventually written to the output file
            //This loop will increment the PC by 4 (4 bytes per instruction) until all instructions have been disassembled
            //It starts at the specified start address, which is also accounted for in the terminating condition
            int instrs = programbits.Count / 32;
            bool mode = false; //After break, set to true, will cause every line after to just print a value rather than an instruction
            
            if(disassemble) //Just disassemble instruction
            {
                for (int PC = StartAddr; PC < (instrs * 4 + StartAddr); PC += 4)
                {
                    int instrindex = (PC - StartAddr) * 8; //Just a convenient value which gives the bit starting index of this instruction
                    List<bool> instruction = programbits.GetRange(instrindex, 32); //Get this 32-bit instruction
                    if (!mode) //Normal mode, inspect each instruction 
                    {
                        //First, print instr in desired format of 6-5-5-5-5-6
                        for (int i = 0; i < 32; i++)
                        {
                            output += instruction[i] ? "1" : "0"; //Append 1 or 0 to string from instruction
                            if (i == 5 || i == 10 || i == 15 || i == 20 || i == 25) //Append space where appropriate
                            {
                                output += " ";
                            }
                        }
                        output += "\t" + PC + "\t"; //Add tab character, PC, then another tab character before opcode
                        Instruction instr = DecodeInstruction(instruction, false);
                        output += instr.op;
                        output += '\t' + instr.opstring;
                        mode = instr.op == "BREAK";
                    }
                    else //Mode after break, simply print binary value in decimal
                    {
                        //First, print instr bits
                        for (int i = 0; i < 32; i++)
                        {
                            output += instruction[i] ? "1" : "0"; //Append 1 or 0 to string from instruction
                        }
                        output += "\t" + PC + "\t"; //Add tab character, PC, then another tab character before int value
                        output += BitsToInt(instruction).ToString(); //Add value of integer 
                    }
                    output += Environment.NewLine;
                }
            }
            else //Trace execution
            {
                //Initialize memory from file
                //Iterate backwards from end until BREAK encountered, or 600 reaches
                for (int PC = (instrs * 4 + StartAddr) - 4; PC >= 600; PC -= 4)
                {
                    int valueindex = (PC - StartAddr) * 8;
                    List<bool> valuebits = programbits.GetRange(valueindex, 32); //Get this 32-bit data value
                    if (DecodeInstruction(valuebits, false).op == "BREAK") //BREAK encountered, stop loading memory
                    {
                        break;
                    }
                    int address = (PC - 600) / 4; //Get actual address
                    ctx.data[address] = BitsToInt(valuebits);
                }
                //Simulate until BREAK in WB stage
                bool nextidstall = false;
                while(true)
                {
                    /*****Simulate execution and update the execution context*****/
                    /***Simulate WB stage***/
                    if (ctx.stageinstrs.WB.op == "BREAK") //BREAK instruction written out, can exit
                    {
                        ctx.breakPC = ctx.stageinstrs.WB.pc;
                        break;
                    }
                    ctx.stageinstrs.WB = ctx.stageinstrs.DS;
                    WriteBack(ctx.stageinstrs.WB, ctx.pipelineregs.DS__WB_ALUout_LMD);
                    /***Simulate DS stage***/
                    ctx.stageinstrs.DS = ctx.stageinstrs.DF;
                    if (ctx.stageinstrs.DS.op == "SW")
                    {
                        WriteMemory(ctx.stageinstrs.DS, ctx.DF_DS);
                        ctx.DF_DS = 0;
                    }
                    if (ctx.stageinstrs.DS.op == "LW")
                    {
                        ctx.DF_DS = ReadMemory(ctx.stageinstrs.DS, ctx.DF_DS);
                    }
                    ctx.pipelineregs.DS__WB_ALUout_LMD = ctx.DF_DS;
                    /***Simulate DF stage***/
                    ctx.stageinstrs.DF = ctx.stageinstrs.EX;
                    CheckDfForwarding();
                    bool printisstall = false;
                    if(ctx.takebranch)
                    {
                        ctx.BranchStall();
                        ctx.takebranch = false;
                        printisstall = true;
                    }
                    ctx.DF_DS = ctx.pipelineregs.EX__DF_ALUout;

                    /***Simulate EX stage***/
                    ctx.stageinstrs.EX = ctx.stageinstrs.RF;
                    CheckExForwarding();
                    ctx.pipelineregs.EX__DF_ALUout = Execute(ctx.stageinstrs.EX);
                    ctx.pipelineregs.EX__DF_B = ctx.pipelineregs.RF__EX_B;
                    /***Simulate RF stage***/
                    if (ctx.stallcnt > 0) //Need to insert stall instructions
                    {
                        ctx.stageinstrs.RF = new Instruction("**STALL**");
                    }
                    else
                    {
                        ctx.stageinstrs.RF = ctx.stageinstrs.ID;
                    }
                    ctx.pipelineregs.RF__EX_A = ctx.registers[(int)ctx.stageinstrs.RF.rs];
                    if (ctx.stageinstrs.RF.op == "SW" || ctx.stageinstrs.RF.op == "LW")
                    {
                        ctx.pipelineregs.RF__EX_A = ctx.registers[(int)ctx.stageinstrs.RF.basereg];
                    }
                    ctx.pipelineregs.RF__EX_B = ctx.registers[(int)ctx.stageinstrs.RF.rt];
                    //Fetch register values
                    /***Simulate ID stage***/
                    if (!(ctx.stallcnt > 0))
                    {
                        if (nextidstall)
                        {
                            ctx.stageinstrs.ID = DecodeInstruction(ctx.stageinstrs.IS, true);
                            nextidstall = false;
                        }
                        else
                        {
                            ctx.stageinstrs.ID = DecodeInstruction(ctx.stageinstrs.IS, printisstall);
                        }
                    }
                    /***Simulate IS stage***/
                    if (!(ctx.stallcnt > 0))
                    {
                        ctx.stageinstrs.IS = ctx.stageinstrs.IF;
                        ctx.pipelineregs.IS__ID_IR = ctx.stageinstrs.IS;
                    }
                    /***Simulate IF stage***/
                    if (!(ctx.stallcnt > 0))
                    {
                        int instrindex = (ctx.PC - StartAddr) * 8; 
                        List<bool> newinstruction = programbits.GetRange(instrindex, 32); //Get this 32-bit instruction
                        ctx.stageinstrs.IF = newinstruction;
                        //Set PC
                        ctx.PC += 4;
                        ctx.pipelineregs.IF__IS_NPC = ctx.PC;
                        //Check for forwarding in ID
                        CheckForwarding();
                    }
                    else
                    {
                        ctx.stallcnt--;
                        ctx.stallinstr = ctx.stallcnt == 0 ? new Instruction("(none)") : ctx.stallinstr;
                        if (ctx.cause == StallCause.Load)
                        {
                            ctx.stalls.Loads++;
                        }
                        else if (ctx.cause == StallCause.Branch)
                        {
                            ctx.stalls.Branches++;
                        }
                        else if (ctx.cause == StallCause.Other)
                        {
                            ctx.stalls.Other++;
                        }
                    }
                    /*****Print information from current execution context after execution on this cycle*****/
                    string cycleoutput = "";
                    cycleoutput += "****Cycle #" + ctx.cycle + "***********************************************" + Environment.NewLine;
                    cycleoutput += "Current PC = " + (ctx.PC - 4) + ":" + Environment.NewLine + Environment.NewLine;
                    //Print info about pipeline, what instruction is in each stage, and stall instruction
                    cycleoutput += "Pipeline Status:" + Environment.NewLine;
                    cycleoutput += "* IF : <unknown>" + Environment.NewLine;
                    string ISstring = "NOP";
                    if (BitsToUInt(ctx.stageinstrs.IS) != 0)
                    {
                        uint instrint = BitsToUInt(ctx.stageinstrs.IS);
                        string hexstring = instrint.ToString("X8");
                        ISstring = "<Fetched: " + hexstring.Substring(0, 2) + " " + hexstring.Substring(2, 2) + " " + hexstring.Substring(4, 2) + " " + hexstring.Substring(6, 2) + ">";
                    }
                    else if (printisstall)
                    {
                        ISstring = "**STALL**";
                        nextidstall = true;
                    }
                    cycleoutput += "* IS : " + ISstring + Environment.NewLine;
                    cycleoutput += "* ID : " + ctx.stageinstrs.ID.FullOp() + Environment.NewLine;
                    cycleoutput += "* RF : " + ctx.stageinstrs.RF.FullOp() + Environment.NewLine;
                    cycleoutput += "* EX : " + ctx.stageinstrs.EX.FullOp() + Environment.NewLine;
                    cycleoutput += "* DF : " + ctx.stageinstrs.DF.FullOp() + Environment.NewLine;
                    cycleoutput += "* DS : " + ctx.stageinstrs.DS.FullOp() + Environment.NewLine;
                    cycleoutput += "* WB : " + ctx.stageinstrs.WB.FullOp() + Environment.NewLine + Environment.NewLine;
                    cycleoutput += "Stall Instruction: " + ctx.stallinstr.FullOp() + Environment.NewLine + Environment.NewLine;
                    //Print forwarding info, putting (none) if the from forward is empty
                    cycleoutput += "Forwarded:" + Environment.NewLine;
                    //Detected print
                    string detectedstr = "(none)";
                    if(ctx.forwarded.From_Detected1.op != "NOP" && ctx.forwarded.From_Detected2.op != "NOP") //2 forwardings detected
                    {
                        detectedstr = "(" + ctx.forwarded.From_Detected1.FullOp() + ") to (" + ctx.forwarded.To_Detected1.FullOp() + ")\n\t(" + ctx.forwarded.From_Detected2.FullOp() + ") to (" + ctx.forwarded.To_Detected2.FullOp() + ")";
                    }
                    else if (ctx.forwarded.From_Detected1.op != "NOP") //1 forwarding detected
                    {
                        detectedstr = "(" + ctx.forwarded.From_Detected1.FullOp() + ") to (" + ctx.forwarded.To_Detected1.FullOp() + ")";
                    }
                    else if (ctx.forwarded.From_Detected2.op != "NOP") //2nd forwarding detected
                    {
                        detectedstr = "(" + ctx.forwarded.From_Detected2.FullOp() + ") to (" + ctx.forwarded.To_Detected2.FullOp() + ")";
                    }
                    cycleoutput += " Detected: " + detectedstr + Environment.NewLine;
                    //Print active forwardings this cycle
                    cycleoutput += " Forwarding:" + Environment.NewLine;
                    string f1str = ctx.forwarded.From_EXDF_RFEX.op == "NOP" ? "(none)" : "(" + ctx.forwarded.From_EXDF_RFEX.FullOp() + ") to (" + ctx.forwarded.To_EXDF_RFEX.FullOp() + ")";
                    cycleoutput += " * EX/DF -> RF/EX : " + f1str + Environment.NewLine;
                    string f2str = ctx.forwarded.From_DFDS_EXDF.op == "NOP" ? "(none)" : "(" + ctx.forwarded.From_DFDS_EXDF.FullOp() + ") to (" + ctx.forwarded.To_DFDS_EXDF.FullOp() + ")";
                    cycleoutput += " * DF/DS -> EX/DF : " + f2str + Environment.NewLine;
                    string f3str = ctx.forwarded.From_DFDS_RFEX.op == "NOP" ? "(none)" : "(" + ctx.forwarded.From_DFDS_RFEX.FullOp() + ") to (" + ctx.forwarded.To_DFDS_RFEX.FullOp() + ")";
                    cycleoutput += " * DF/DS -> RF/EX : " + f3str + Environment.NewLine;
                    string f4str = ctx.forwarded.From_DSWB_EXDF.op == "NOP" ? "(none)" : "(" + ctx.forwarded.From_DSWB_EXDF.FullOp() + ") to (" + ctx.forwarded.To_DSWB_EXDF.FullOp() + ")";
                    cycleoutput += " * DS/WB -> EX/DF : " + f4str + Environment.NewLine;
                    string f5str = ctx.forwarded.From_DSWB_RFEX.op == "NOP" ? "(none)" : "(" + ctx.forwarded.From_DSWB_RFEX.FullOp() + ") to (" + ctx.forwarded.To_DSWB_RFEX.FullOp() + ")";
                    cycleoutput += " * DS/WB -> RF/EX : " + f5str + Environment.NewLine + Environment.NewLine;
                    //Print info about pipeline registers
                    cycleoutput += "Pipeline Registers:" + Environment.NewLine;
                    cycleoutput += "* IF/IS.NPC\t\t: " + ctx.pipelineregs.IF__IS_NPC + Environment.NewLine;
                    cycleoutput += "* IS/ID.IR\t\t: " + ((ISstring == "NOP" || ISstring == "**STALL**") ? "<00 00 00 00>" : ISstring.Replace("Fetched: ", "")) + Environment.NewLine;
                    cycleoutput += "* RF/EX.A\t\t: " + ctx.pipelineregs.RF__EX_A + Environment.NewLine;
                    cycleoutput += "* RF/EX.B\t\t: " + ctx.pipelineregs.RF__EX_B + Environment.NewLine;
                    cycleoutput += "* EX/DF.ALUout\t: " + ctx.pipelineregs.EX__DF_ALUout + Environment.NewLine;
                    cycleoutput += "* EX/DF.B\t\t: " + ctx.pipelineregs.EX__DF_B + Environment.NewLine;
                    cycleoutput += "* DS/WB.ALUout-LMD\t: " + ctx.pipelineregs.DS__WB_ALUout_LMD + Environment.NewLine + Environment.NewLine;

                    //Print regs, memory, and summary information so far
                    cycleoutput += FinalPrintings();

                    //If cycles not specified add; don't add if both are 0; else add if within specified range
                    if (startcyle == -1 || !(startcyle == 0 && endcycle == 0) && (startcyle <= ctx.cycle && ctx.cycle <= endcycle))
                    {
                        output += cycleoutput;
                    }
                    ctx.cycle++;
                }

                //Write summary info
                output += "**** Summary ************************************************" + Environment.NewLine + Environment.NewLine;
                output += "BREAK PC = " + ctx.breakPC + Environment.NewLine + Environment.NewLine;
                output += "Total Cycles Simulated = " + (ctx.cycle-1) + Environment.NewLine + Environment.NewLine;
                output += FinalPrintings();

            }
            if(debug)
            {
                Console.WriteLine(output);
                File.WriteAllText("output.txt", output); //Write output to file
            }
            else
            {
                File.WriteAllText(outputpath, output); //Write output to file
            }
        }

        static string FinalPrintings()
        {
            string output = "";

            //Print contents of each integer register
            output += "Integer registers:" + Environment.NewLine;
            for (int i = 0; i < 32; i++)
            {
                output += "R" + i + "\t" + ctx.registers[i] + "\t";
                if ((i + 1) % 4 == 0)
                {
                    output += Environment.NewLine;
                }
            }
            //Display memory contents
            output += Environment.NewLine + "Data memory:" + Environment.NewLine;
            for (int i = 0; i < 10; i++)
            {
                output += (i * 4 + 600) + ": " + ctx.data[i] + Environment.NewLine;
            }
            //Display stall info
            output += Environment.NewLine + "Total Stalls:" + Environment.NewLine;
            output += "*Loads\t: " + ctx.stalls.Loads + Environment.NewLine;
            output += "*Branches: " + ctx.stalls.Branches + Environment.NewLine;
            output += "*Other\t: " + ctx.stalls.Other + Environment.NewLine + Environment.NewLine;
            //Display forwarding info
            output += "Total Forwardings:" + Environment.NewLine;
            output += "* EX/DF -> RF/EX : " + ctx.forwards.EXDF_RFEX + Environment.NewLine;
            output += "* DF/DS -> EX/DF : " + ctx.forwards.DFDS_EXDF + Environment.NewLine;
            output += "* DF/DS -> RF/EX : " + ctx.forwards.DFDS_RFEX + Environment.NewLine;
            output += "* DS/WB -> EX/DF : " + ctx.forwards.DSWB_EXDF + Environment.NewLine;
            output += "* DS/WB -> RF/EX : " + ctx.forwards.DSWB_RFEX + Environment.NewLine + Environment.NewLine;

            return output;
        }

    }
}
