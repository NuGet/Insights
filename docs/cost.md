# Performance and cost

This is a log of dollar and time cost for running the [drivers](../README.md#Drivers) in this project.

*As of February 2021*

Tested timestamp range:
- Min: `2015-02-01T06:22:45.8488496Z`
- Max: `2021-02-05T15:55:33.3608941Z`

Results:
- `FindPackageFile`
  - Runtime: 37 minutes, 19 seconds
  - Total cost: $3.37

<details>
<summary>Detailed cost</summary>

- Azure Functions cost - $2.77
  - bandwidth / data transfer out - $1.62
  - functions / execution time - $1.13
  - functions / total executions - $0.01
- Azure Storage cost - $0.60
  - storage / tables / scan operations - $0.26
  - storage / tables / batch write operations - $0.15
  - storage / queues v2 / lrs class 1 operations - $0.13
  - storage / tiered block blob / all other operations - $0.01
  - storage / files / protocol operations - $0.01

</details>

- `FindPackageSignature`
  - Runtime: 1 hour, 11 minutes, 29 seconds
  - Total cost: $6.30

<details>
<summary>Detailed cost</summary>

- Azure Functions cost - $4.97
  - functions / execution time - $4.14
  - bandwidth / data transfer out - $0.81
  - functions / total executions - $0.02
- Azure Storage cost - $1.33
  - storage / tables / batch write operations - $0.36
  - storage / tables / scan operations - $0.26
  - storage / queues v2 / lrs class 1 operations - $0.14
  - storage / tables / delete operations - $0.13
  - storage / tables / write operations - $0.13
  - storage / tiered block blob / all other operations - $0.05
  - storage / files / protocol operations - $0.04
  - storage / queues v2 / class 2 operations - $0.04
  - storage / files / lrs write operations - $0.02
  - storage / tables / read operations - $0.01
  - storage / tables / lrs class 1 additional io - $0.01

</details>

- `FindPackageAsset`
  - Runtime: 41 minutes, 34 seconds
  - Total cost: $5.61

<details>
<summary>Detailed cost</summary>

- Azure Functions cost - $4.11
  - functions / execution time - $2.52
  - bandwidth / data transfer out - $1.57
  - functions / total executions - $0.02
- Azure Storage cost - $1.50
  - storage / tables / batch write operations - $0.35
  - storage / tables / scan operations - $0.25
  - storage / queues v2 / lrs class 1 operations - $0.13
  - storage / tables / delete operations - $0.14
  - storage / tables / write operations - $0.14
  - storage / files / lrs write operations - $0.24
  - storage / files / protocol operations - $0.20
  - storage / tiered block blob / all other operations - $0.01
  - storage / queues v2 / class 2 operations - $0.02
  - storage / tables / read operations - $0.01
  - storage / tables / lrs class 1 additional io - $0.01

</details>

- `FindPackageAssembly`
  - Runtime: 1 hour, 33 minutes, 25 seconds
  - Total cost: $6.37

<details>
<summary>Detailed cost</summary>

- Azure Functions cost - $6.37
  - functions / execution time - $0.63
  - bandwidth / data transfer out - $0.87
  - functions / total executions - $0.04
- Azure Storage cost - $4.74
  - storage / queues v2 / lrs class 1 operations - $3.08
  - storage / files / lrs write operations - $0.48
  - storage / tables / batch write operations - $0.35
  - storage / tables / scan operations - $0.24
  - storage / tiered block blob / all other operations - $0.14
  - storage / tables / delete operations - $0.13
  - storage / tables / write operations - $0.13
  - storage / tables / read operations - $0.13
  - storage / files / protocol operations - $0.01
  - storage / files / read operations - $0.01
  - storage / queues v2 / class 2 operations - $0.01
  - storage / tables / lrs class 1 additional io - $0.01

</details>
