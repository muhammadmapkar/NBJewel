// Role: Describes one entry of the design master list that feeds the design
// code dropdown. "Master chit" is assumed to mean this design master.

export interface Design {
  designId: number;
  designCode: string;
  name: string;
}
