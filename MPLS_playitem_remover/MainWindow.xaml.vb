Class MainWindow
    Dim startindex As ULong
    Dim filecontent As Byte()
    Dim playlist_start_address As ULong
    Dim playlist_mark_start_address As ULong
    Dim length_of_the_playlist_section As ULong
    Dim number_of_play_items As UInteger
    Dim playlist_items As New List(Of Playlist_item)
    Dim dialogue As New Microsoft.Win32.OpenFileDialog
    Private Sub Button_Click(sender As Object, e As RoutedEventArgs)
        dialogue.Filter = "MPLS playlist (*.mpls)|*.mpls"
        If dialogue.ShowDialog = True Then
            m2tsList.Items.Clear()
            filecontent = My.Computer.FileSystem.ReadAllBytes(dialogue.FileName)
            playlist_start_address = filecontent(8) * 16777216 + filecontent(9) * 65536 + filecontent(10) * 256 + filecontent(11)
            playlist_mark_start_address = filecontent(12) * 16777216 + filecontent(13) * 65536 + filecontent(14) * 256 + filecontent(15)
            length_of_the_playlist_section = filecontent(playlist_start_address) * 16777216 + filecontent(playlist_start_address + 1) * 65536 + filecontent(playlist_start_address + 2) * 256 + filecontent(playlist_start_address + 3)
            number_of_play_items = filecontent(playlist_start_address + 6) * 256 + filecontent(playlist_start_address + 7)
            startindex = playlist_start_address + 10
            For i As UInteger = 1 To number_of_play_items
                Dim item As New Playlist_item
                item.length = filecontent(startindex) * 256 + filecontent(startindex + 1)
                item.content = New Byte(item.length) {}
                startindex += 2
                For j As ULong = 0 To item.length - 1
                    item.content(j) = filecontent(startindex + j)
                Next
                For j = 0 To 8
                    item.filename &= Convert.ToChar(item.content(j))
                Next
                startindex += item.length
                m2tsList.Items.Add(item.filename)
                playlist_items.Add(item)
            Next
        End If
    End Sub

    Private Sub removeButton_Click(sender As Object, e As RoutedEventArgs) Handles removeButton.Click
        'I know these part is impossible to read... You need to read MPLS file format and try to understand the code here line by line
        '(It's also very painful to figure out how to write this function.)
        'https://en.wikibooks.org/wiki/User:Bdinfo/mpls
        'Basically all this function do is to copy the original MPLS file and change some things to new value on the fly.

        Dim newfile = New Byte(filecontent.LongCount - playlist_items.Item(m2tsList.SelectedIndex).length - 3) {}
        Array.Copy(filecontent, 0, newfile, 0, 8)
        Array.Copy(convertUlongToHex(playlist_start_address, 4), 0, newfile, 8, 4)
        Array.Copy(convertUlongToHex(playlist_mark_start_address - playlist_items.Item(m2tsList.SelectedIndex).length - 2, 4), 0, newfile, 12, 4)
        Dim tmp = New Byte(playlist_start_address - 16) {}
        Array.Copy(filecontent, 16, tmp, 0, Long.Parse(playlist_start_address - 16))
        Array.Copy(tmp, 0, newfile, 16, tmp.Length)
        Array.Copy(convertUlongToHex(length_of_the_playlist_section - playlist_items.Item(m2tsList.SelectedIndex).length - 2, 4), 0, newfile, Long.Parse(playlist_start_address), 4)
        Array.Copy(filecontent, Long.Parse(playlist_start_address + 4), newfile, Long.Parse(playlist_start_address + 4), 2)
        Array.Copy(convertUlongToHex(number_of_play_items - 1, 2), 0, newfile, Long.Parse(playlist_start_address + 6), 2)
        Array.Copy(filecontent, Long.Parse(playlist_start_address + 8), newfile, Long.Parse(playlist_start_address + 8), 2)
        Dim startindex As Long = playlist_start_address + 10
        For i = 0 To number_of_play_items - 1
            If playlist_items(i).filename <> m2tsList.SelectedValue Then
                Array.Copy(convertUlongToHex(playlist_items(i).length, 2), 0, newfile, startindex, 2)
                Array.Copy(playlist_items(i).content, 0, newfile, startindex + 2, Long.Parse(playlist_items(i).length))
                startindex += playlist_items(i).length + 2
            End If
        Next
        tmp = New Byte(filecontent.Length - startindex - playlist_items.Item(m2tsList.SelectedIndex).length - 2) {}
        Array.Copy(filecontent, Long.Parse(startindex + 2 + playlist_items.Item(m2tsList.SelectedIndex).length), tmp, 0, newfile.Length - startindex - 2)
        Array.Copy(tmp, 0, newfile, startindex, newfile.Length - startindex)

        Dim newfileLocation = dialogue.FileName.Substring(0, dialogue.FileName.Length - dialogue.SafeFileName.Length) & "modified.mpls"
        If My.Computer.FileSystem.FileExists(newfileLocation) Then
            My.Computer.FileSystem.DeleteFile(newfileLocation)
        End If
        My.Computer.FileSystem.WriteAllBytes(newfileLocation, newfile, False)
        MsgBox("Creadted ""modified.m2ts"" in playlist folder.")
    End Sub

    Private Sub m2tsList_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles m2tsList.SelectionChanged
        removeButton.IsEnabled = m2tsList.SelectedIndex > -1
    End Sub

    Function convertUlongToHex(num As ULong, length As Integer) As Byte()
        ' Input    output
        ' 100, 2 -> (nul)d
        ' 58, 4 -> (nul)(nul)(nul):
        ' 484, 4 -> (nul)(nul)1α
        Dim hex = num.ToString("X" & length * 2)
        Const hexTable = "0123456789ABCDEF"
        Dim output = New Byte(hex.Length / 2 - 1) {}
        For i = 0 To hex.Length - 1 Step 2
            output(i / 2) = Convert.ToByte(ChrW(hexTable.IndexOf(hex(i)) * 16 + hexTable.IndexOf(hex(i + 1))))
        Next
        Return output
    End Function
End Class

Class Playlist_item
    Public length As ULong,
        content As Byte(),
        filename As String = ""
End Class
