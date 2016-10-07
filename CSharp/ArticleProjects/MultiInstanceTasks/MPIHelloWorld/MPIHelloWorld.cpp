// MPIHelloWorld.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include "mpi.h"
#include "stdio.h"
#include "stdlib.h"

int main(int argc, char* argv[])
{
	MPI_Init(&argc, &argv);

	int rank, size;

	MPI_Comm_rank(MPI_COMM_WORLD, &rank);
	MPI_Comm_size(MPI_COMM_WORLD, &size);
	if (rank == 0)
	{
		char helloStr[] = "Hello world";
		for (int i = 1; i < size; ++i) {
			MPI_Send(helloStr, _countof(helloStr), MPI_CHAR, i, 0, MPI_COMM_WORLD);
		}
	}
	else if (rank > 0)
	{
		char helloStr[12];
		MPI_Recv(helloStr, _countof(helloStr), MPI_CHAR, 0, 0, MPI_COMM_WORLD, MPI_STATUSES_IGNORE);
		printf("Rank %d received string \"%s\" from Rank 0\n", rank, helloStr);
	}

	MPI_Finalize();

	return 0;
}
