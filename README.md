# MIPS Simulator

## Use
To run the program, navigate to the Application directory in Windows CMD and run:

`MIPSSimulator.exe Inputfilename Outputfilename sim|dis [-Tm:n]`

* sim: Runs a full pipeline simulation on the input program.
* dis: Simply disassembles the program.

* -Tm:n is optional, if not provided the simulation trace will display all cycles, otherwise will display from cycles m to n (inclusive).
* -T0:0 will display the summary only.

For example, the following command will run a simulation on the input, and output cycles 5 to 10 (inclusive) as well as the summary.
`MIPSSimulator.exe input.txt output.txt sim -T5:10`

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

## Samples

A sample input file, [input.txt](input.txt), has been provided with the repository. There are also two sample outputs: 
[One for the disassembly of this sample input](dis-output.txt), [and one with the simulation trace of the input](sim-output.txt).
