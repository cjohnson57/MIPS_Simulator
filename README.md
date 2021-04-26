# MIPS Simulator

## Use
To run the program, navigate to the Application directory in Windows CMD and run:

`MIPSSimulator.exe Inputfilename Outputfilename sim|dis [-Tm:n]`

* sim: Runs a full pipeline simulation on the input program.
* dis: Simply disassembles the program.

* -Tm:n is optional, if not provided the simulation trace will display all cycles, otherwise will display from cycles m to n (inclusive).
* -T0:0 will display the summary only.

For example, the following command will run a simulation on the input, and output cycles 5 to 10 (inclusive) as well as the summary:

```MIPSSimulator.exe input.txt output.txt sim -T5:10```

If not enough arguments are specified or the input file does not exist, it will display an error and information.

If you don't specify .txt for the output file, it will be added automatically.

If the operation is not specified as "sim" or "dis", it will display an error and information.

If the -T argument is provided but the format is not valid, it will display an error and information.

The file given in Inputfilename will be run through, construct the output of either the program disassembly or execution trace, and write that output to Outputfilename.

The input must be a file of the machine code (1s and 0s) of valid MIPS instructions to work correctly, with the following specifications:
>Your program will be given a text input file. This file will contain a sequence of 32-bit instruction words which begin at address "496". 
>The final instruction in the sequence of instructions is always BREAK. The data section is followed the BREAK instruction and begins at address "700". 
>Following that is a sequence of 32-bit 2's complement signed integers for the program data up to the end of file. Note that the instruction words always 
>start at address "496". If the instructions occupy beyond address "700", the data region will be followed the BREAK instruction immediately.

## Code Description

[Classes.cs:](Classes.cs) This file defines the classes and enums used throughout the project. Two two most important classes here are Instruction and ExecutionContext,
most of the other classes are mainly for storage of values and statistics.

Instruction stores all decoded information about a MIPS instruction including its opcode, register values, and constants.

ExecutionContext stores all information about the running simulation including all register and data values, the PC, cycle count, 
values for the instructions in each stage and pipeline registers, current stalls, and statistics used for the summary.

[Program.cs:](Program.cs) This is the main logic of the program including handling arguments and file IO, and construction of the output file.

For disassembly, this file controls the logic for going through each instruction, decoding it, and adding the decoded instruction to the output.

For simulation, it first handles the logic of each pipeline stage (working backwards from the WB stage), checking for forwarding, and stalls if necessary.
This portion of the code is mainly where the functions in the other code files are called.

After handling each stage, there is a large block of code to construct the output of each cycle including outputting what instruction is in each stage,
values in registers and memory, any detected forwarding opportunities, and statistics about the execution so far including total stalls and forwards.

After the simulation completes (indicated by the BREAK instruction exiting the WB stage) a summary is displayed which includes the PC of the break instruction,
how many total cycles were simulated, and the overall statistics of the simulation.

[ForwardingDetection.cs:](ForwardingDetection.cs) Here are the functions called by Program.cs when forwarding must be checked for. 
There are only three stages which must check for forwarding:
* ID: To check for hazards which can cause a stall.
* EX: To check for forwards to the execution stage just in time for the value to be needed, ex, if an ADD instruction needs R0 and R0 is set in the previous instruction.
* DF: To check for forwards to the DF stage, in particular, a SW instruction where the value to be stored is set in the previous 2 instructions.

[PipelineInstructions.cs:](PipelineInstructions.cs) This is where the pipeline-related functions called by Program.cs are, including
writing back data values, writing and reading memory, resolving branches, and ALU execution.

[HelperFunctions.cs:](HelperFunctions.cs) This file has functions for converting between bits and integers as well as the large decode function,
where the binary values of a machine code instruction are inspected in order to find the opcode and properties of each instruction.

## Pipeline Description

This project assumes an 8-stage MIPS pipeline with the following stages:

* IF - First half of instruction fetch. PC (Program Counter) selection actually happens here, together with initiation of instruction cache access.
* IS - Second half of instruction fetch, complete instruction cache access.
* ID - Instruction decode, hazard checking.
* RF - Register fetch.
* EX - Execution, which includes effective address calculation, ALU operation, and branch-target computation and condition evaluation.
* DF - Data fetch, first half of data cache access.
* DS - Second half of data fetch, completion of data cache access. Note that the data access always hit the data cache.
* WB - Write back for loads and register-register operations.

The following instructions are supported as specified in the [MIPS Instruction Set Architecture](https://www.ece.lsu.edu/lpeng/ee7700-2/mips.pdf):
* J, JR, BEQ, BNE, BGEZ, BGTZ, BLEZ, BLTZ
* ADDI, ADDIU
* BREAK
* SLT
* SW, LW
* SLL, SRL, SRA
* SUB, SUBU, ADD, ADDU
* AND, OR, XOR, NOR
* SLTI
* NOP

There are two potential stalls:

1. If a value is loaded by a LW instruction which is needed in the next instruction, there will be 2 stall cycles. If it's needed 2 instructions later, just 1 stall cycle.
2. Branches are always predicted as not taken. If they are taken, the pipeline must be cleared up to the EX stage and there will be a 4 cycle stall.

The following forwardings can occur:

* To the EX stage from the DF, DS, and WB stages.
* To the DF stage from the DS and WB stages.

## Samples

A sample input file, [input.txt](input.txt), has been provided with the repository. There are also two sample outputs: 
[One for the disassembly of this sample input](dis-output.txt), [and one with the simulation trace of the input](sim-output.txt).
