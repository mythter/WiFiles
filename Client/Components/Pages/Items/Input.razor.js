export class Input {

    static InitPattern(input, pattern) {
        input.oninput = (e) => {
            console.log("input");
            let cursorPos = e.target.selectionStart
            let currentValue = e.target.value
            let cleanValue = currentValue.replace(/\D/g, "");
            let formatInput = this.patternMatch({
                input: cleanValue,
                template: pattern
            });

            e.target.value = formatInput

            let isBackspace = (e?.data == null)
            let nextCusPos = this.nextDigit(formatInput, cursorPos, isBackspace)

            input.setSelectionRange(nextCusPos + 1, nextCusPos + 1);
        };
    }

    static nextDigit(input, cursorpos, isBackspace) {
        if (isBackspace) {
            for (let i = cursorpos - 1; i > 0; i--) {
                if (/\d/.test(input[i])) {
                    return i
                }
            }
        } else {
            for (let i = cursorpos - 1; i < input.length; i++) {
                if (/\d/.test(input[i])) {
                    return i
                }
            }
        }
    
        return cursorpos
    }
    
    static patternMatch({
        input,
        template
    }) {
        try {
    
            let j = 0;
            let plaintext = "";
            let countj = 0;
            while (j < template.length) {
    
                if (countj > input.length - 1) {
                    template = template.substring(0, j);
                    break;
                }
    
                if (template[j] == input[j]) {
                    j++;
                    countj++;
                    continue;
                }
    
                if (template[j] == "_") {
                    template = template.substring(0, j) + input[countj] + template.substring(j + 1);
                    plaintext = plaintext + input[countj];
                    countj++;
                }
                j++;
            }
    
            return template
    
        } catch {
            return ""
        }
    }
}
