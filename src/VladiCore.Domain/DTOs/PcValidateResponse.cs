using System.Collections.Generic;

namespace VladiCore.Domain.DTOs;

public class PcValidateResponse
{
    public bool IsCompatible { get; set; }

    public IList<PcValidationIssueDto> Issues { get; set; } = new List<PcValidationIssueDto>();
}
