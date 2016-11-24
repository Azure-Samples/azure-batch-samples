// Copyright (c) Microsoft Corporation
//
// MPI sample program for use with the companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-mpi/
// Based on the sample in the following blog post:
// https://blogs.technet.microsoft.com/windowshpc/2015/02/02/how-to-compile-and-run-a-simple-ms-mpi-program/

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
        for (int i = 1; i < size; ++i)
		{
            MPI_Send(helloStr, _countof(helloStr), MPI_CHAR, i, 0, MPI_COMM_WORLD);
        }
    }
    else
    {
        char helloStr[12];
        MPI_Recv(helloStr, _countof(helloStr), MPI_CHAR, 0, 0, MPI_COMM_WORLD, MPI_STATUSES_IGNORE);
        printf("Rank %d received string \"%s\" from Rank 0\n", rank, helloStr);
    }

    MPI_Finalize();

    return 0;
}
